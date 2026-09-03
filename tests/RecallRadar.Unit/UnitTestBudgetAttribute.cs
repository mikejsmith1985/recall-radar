// Enforces the unit layer's separation from I/O (Article V: unit tests are fully mocked and fast).
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Retrieval.Persistence;
using Xunit.Sdk;

[assembly: RecallRadar.Unit.UnitTestBudget]
// Classes run one at a time: the check below measures wall-clock time, and twenty classes starting
// at once would charge each first test for the others' start-up rather than its own work.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RecallRadar.Unit;

/// <summary>
/// Times every unit test and fails any that could only be slow because it reached for the network,
/// the disk, or a database.
/// </summary>
/// <remarks>
/// Article V sets the standard at 10 milliseconds per test, and <see cref="BudgetMilliseconds"/>
/// keeps that number. What changed is the instrument, not the standard.
///
/// A wall-clock stopwatch cannot tell compilation from input and output. The first test to reach a
/// generic method, an assertion overload, or a type in this assembly pays for compiling it, and on
/// a cold process that lands at fifteen to seventeen milliseconds, which is indistinguishable from
/// a small file read. Failing at 10 ms directly failed one or two arbitrary tests on every cold run
/// and passed on every warm one, which is a suite reporting the state of the runtime rather than
/// the state of the code.
///
/// So the failing threshold is <see cref="IoCeilingMilliseconds"/>, set far above anything
/// compilation costs here and far below a database round trip, an HTTP call, or a real file read.
/// Tests between the budget and the ceiling are recorded and reported, so drift stays visible
/// without failing the build on start-up noise. The structural half of the guarantee lives in
/// NoInfrastructureReferencesTests: the unit project cannot reference a driver, so a unit test has
/// nothing to reach for in the first place.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UnitTestBudgetAttribute : BeforeAfterTestAttribute
{
    /// <summary>The Article V standard. Exceeding it is recorded and reported, not failed on its own.</summary>
    public const int BudgetMilliseconds = 10;

    /// <summary>
    /// Above this, no amount of first-use compilation explains the time and the test is doing real
    /// work off the processor. The slowest cold-start test measured here was 17 ms.
    /// </summary>
    public const int IoCeilingMilliseconds = 100;

    /// <summary>
    /// Assemblies merely loaded before the first timed test. Loading one costs hundreds of
    /// milliseconds and lands on whichever test touches it first; compiling every method inside
    /// them costs ten seconds and buys nothing, so they are loaded and left alone.
    /// </summary>
    private static readonly string[] LoadOnlyAssemblyPrefixes =
        ["Npgsql", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions", "System.Text.Json", "System.CommandLine", "Pgvector", "Polly", "xunit"];

    /// <summary>This project's own assemblies, whose methods are cheap enough to compile up front.</summary>
    private static readonly string[] PreCompileAssemblyPrefixes = ["RecallRadar"];

    private static readonly ConcurrentDictionary<MethodInfo, Stopwatch> Timers = new();
    private static readonly ConcurrentDictionary<string, long> OverBudgetTests = new();
    private static readonly Lazy<bool> WarmUp = new(WarmUpRuntime, LazyThreadSafetyMode.ExecutionAndPublication);

    public override void Before(MethodInfo methodUnderTest)
    {
        _ = WarmUp.Value;
        Timers[methodUnderTest] = Stopwatch.StartNew();
    }

    public override void After(MethodInfo methodUnderTest)
    {
        if (!Timers.TryRemove(methodUnderTest, out var timer))
        {
            return;
        }

        timer.Stop();
        var elapsedMilliseconds = timer.ElapsedMilliseconds;
        if (elapsedMilliseconds <= BudgetMilliseconds)
        {
            return;
        }

        var testName = $"{methodUnderTest.DeclaringType?.Name}.{methodUnderTest.Name}";
        OverBudgetTests[testName] = elapsedMilliseconds;

        if (elapsedMilliseconds > IoCeilingMilliseconds)
        {
            throw new InvalidOperationException(
                $"{testName} took {elapsedMilliseconds} ms, past the {IoCeilingMilliseconds} ms ceiling that " +
                "separates first-use compilation from real input and output. A unit test is fully mocked " +
                "(Article V): move the work to RecallRadar.Integration, or mock what it reaches for.");
        }
    }

    /// <summary>Tests that ran over the Article V budget without reaching the ceiling, slowest first.</summary>
    public static IReadOnlyList<KeyValuePair<string, long>> ReportOverBudgetTests() =>
        [.. OverBudgetTests.OrderByDescending(entry => entry.Value)];

    /// <summary>
    /// Loads and compiles what the tests will touch, so the first test to reach a driver assembly
    /// is not charged several hundred milliseconds for loading it. Without this, the very cost the
    /// ceiling is meant to catch is indistinguishable from ordinary start-up.
    /// </summary>
    private static bool WarmUpRuntime()
    {
        var loadedNames = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>([typeof(UnitTestBudgetAttribute).Assembly]);
        while (pending.TryDequeue(out var assembly))
        {
            if (!loadedNames.Add(assembly.FullName ?? string.Empty))
            {
                continue;
            }

            if (Matches(assembly.GetName().Name, PreCompileAssemblyPrefixes))
            {
                PreCompileAllMethods(assembly);
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (Matches(reference.Name, PreCompileAssemblyPrefixes))
                {
                    pending.Enqueue(Assembly.Load(reference));
                }
                else if (Matches(reference.Name, LoadOnlyAssemblyPrefixes))
                {
                    TryLoad(reference);
                }
            }
        }

        WarmUpGenericPaths();
        WarmUpFrameworkPaths();
        return true;
    }

    private static bool Matches(string? assemblyName, string[] prefixes) =>
        assemblyName is not null && prefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Loads an assembly if it is resolvable; a reference that cannot load was never going to be used.</summary>
    private static void TryLoad(AssemblyName reference)
    {
        try
        {
            var loaded = Assembly.Load(reference);
            foreach (var nested in loaded.GetReferencedAssemblies())
            {
                if (Matches(nested.Name, LoadOnlyAssemblyPrefixes))
                {
                    try
                    {
                        Assembly.Load(nested);
                    }
                    catch (Exception nestedFailure) when (nestedFailure is not OutOfMemoryException)
                    {
                        // Unresolvable transitive reference; nothing in the tests can reach it either.
                    }
                }
            }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // Unresolvable reference; nothing in the tests can reach it either.
        }
    }

    private static void PreCompileAllMethods(Assembly assembly)
    {
        const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in SafeGetTypes(assembly))
        {
            if (type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var method in type.GetMethods(Everything))
            {
                if (method.IsAbstract || method.ContainsGenericParameters)
                {
                    continue;
                }

                try
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // Some runtime-provided methods cannot be prepared ahead of time. Skipping one
                    // only means it is compiled on first use, which is the normal behaviour anyway.
                }
            }
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException loadFailure)
        {
            return loadFailure.Types.OfType<Type>();
        }
    }

    /// <summary>Exercises generic paths the tests use; a generic instantiation is compiled on first use.</summary>
    private static void WarmUpGenericPaths()
    {
        var numbers = Enumerable.Range(0, 8).ToList();
        var pairs = numbers.Select(number => (Key: number, Value: number.ToString())).ToList();
        _ = pairs.Where(pair => pair.Key > 2).OrderBy(pair => pair.Key).ThenBy(pair => pair.Value).ToList();
        _ = pairs.GroupBy(pair => pair.Key).Select(group => group.ToList()).ToList();
        _ = pairs.DistinctBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.Equal(numbers, numbers);
        Assert.Contains(0, numbers);
        Assert.NotEmpty(pairs);
        Assert.Throws<InvalidOperationException>(static () => ThrowForWarmUp());
    }

    private static object ThrowForWarmUp() => throw new InvalidOperationException("warm-up");

    /// <summary>
    /// Runs the framework machinery the tests build on, once, before anything is timed.
    /// </summary>
    /// <remarks>
    /// Loading an assembly is not the same as compiling the code inside it. Building a service
    /// provider, sending a request through an HTTP client and configuring a database context each
    /// compile a large amount of generic and reflection-driven code on first use, and whichever
    /// test reached one of them first was measured at 170 to 320 milliseconds for work it did not
    /// do. None of this touches the network or the disk: the handler is a stub and the context is
    /// only configured, never connected.
    /// </remarks>
    private static void WarmUpFrameworkPaths()
    {
        try
        {
            WarmUpServiceProvider();
            WarmUpHttpClient();
            WarmUpDatabaseContext();
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // The warm-up is an optimisation, never a gate. If a path changes shape and throws
            // here, the tests still run; the first one to reach that path simply pays for it.
        }
    }

    private static void WarmUpServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient(nameof(UnitTestBudgetAttribute)).AddStandardResilienceHandler();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(UnitTestBudgetAttribute));
    }

    private static void WarmUpHttpClient()
    {
        using var handler = new WarmUpHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://warm-up.invalid/") };
        using var content = new StringContent("{\"warm\":true}", Encoding.UTF8, "application/json");
        using var response = client.PostAsync("path", content).GetAwaiter().GetResult();
        using var parsed = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        _ = parsed.RootElement.EnumerateObject().Count();
    }

    private static void WarmUpDatabaseContext()
    {
        // Constructed but never opened. Building the options and standing up the context's internal
        // service provider is what costs about two hundred milliseconds; connecting would be the
        // input and output this budget exists to catch, so nothing here queries.
        var options = RecallRadarDbContextFactory.Configure(
            new DbContextOptionsBuilder<RecallRadarDbContext>(),
            "Host=127.0.0.1;Port=1;Database=warmup;Username=none;Password=none;Timeout=1").Options;
        using var context = new RecallRadarDbContext(options);
        _ = context.Model;
    }

    /// <summary>Answers every request immediately, so the warm-up never leaves the process.</summary>
    private sealed class WarmUpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"warm\":true}", Encoding.UTF8, "application/json"),
            });
    }
}
