// Enforces the unit-test speed budget the constitution sets (Article V: mocked, under 10 ms).
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Sdk;

[assembly: RecallRadar.Unit.UnitTestBudget]

namespace RecallRadar.Unit;

/// <summary>
/// Times every unit test and fails any that exceeds the budget. A unit test that reaches for
/// the network, the disk, or a database blows this budget, so the budget is what keeps the
/// layers honest rather than a naming convention.
/// </summary>
/// <remarks>
/// The budget is meant to catch I/O, not the runtime's one-off cost of loading assemblies and
/// compiling code the first time it runs. Those costs are paid once, before the first test is
/// timed, by <see cref="WarmUpRuntime"/>; what remains inside the timer is the test's own work.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UnitTestBudgetAttribute : BeforeAfterTestAttribute
{
    public const int BudgetMilliseconds = 10;

    /// <summary>Assemblies whose code is compiled ahead of the first timed test. Framework assemblies are left to load lazily.</summary>
    private static readonly string[] WarmUpAssemblyPrefixes = ["RecallRadar", "System.CommandLine", "Pgvector"];

    private static readonly ConcurrentDictionary<MethodInfo, Stopwatch> Timers = new();
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
        if (timer.ElapsedMilliseconds <= BudgetMilliseconds)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{methodUnderTest.DeclaringType?.Name}.{methodUnderTest.Name} took {timer.ElapsedMilliseconds} ms, " +
            $"over the {BudgetMilliseconds} ms unit budget required by constitution Article V. " +
            "Move the slow work to RecallRadar.Integration, or mock what it reaches for.");
    }

    /// <summary>Loads and pre-compiles the project and parser assemblies so the timer measures behaviour, not start-up.</summary>
    private static bool WarmUpRuntime()
    {
        var loaded = new HashSet<string>();
        var pending = new Queue<Assembly>([typeof(UnitTestBudgetAttribute).Assembly]);
        while (pending.TryDequeue(out var assembly))
        {
            if (!loaded.Add(assembly.FullName ?? assembly.GetName().Name ?? string.Empty))
            {
                continue;
            }

            if (ShouldPreCompile(assembly))
            {
                PreCompileAllMethods(assembly);
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (WarmUpAssemblyPrefixes.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
                {
                    pending.Enqueue(Assembly.Load(reference));
                }
            }
        }

        return true;
    }

    private static bool ShouldPreCompile(Assembly assembly) =>
        WarmUpAssemblyPrefixes.Any(prefix => assembly.GetName().Name?.StartsWith(prefix, StringComparison.Ordinal) == true);

    private static void PreCompileAllMethods(Assembly assembly)
    {
        const BindingFlags everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var method in type.GetMethods(everything))
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
                    // Some runtime-provided methods cannot be prepared ahead of time; skipping them only
                    // means that method is compiled on first use, which is the normal behaviour anyway.
                }
            }
        }
    }
}
