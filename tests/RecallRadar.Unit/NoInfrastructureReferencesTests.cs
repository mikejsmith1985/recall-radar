// Proves structurally that the unit layer cannot reach infrastructure (Article V).
using System.Reflection;

namespace RecallRadar.Unit;

/// <summary>
/// The timing check in <see cref="UnitTestBudgetAttribute"/> is a backstop; this is the guarantee.
/// A unit test cannot open a database connection, start a container, or serve a stub HTTP response
/// if the assembly holding it never loads a driver to do so. Those belong to
/// RecallRadar.Integration, which is where real infrastructure is allowed.
/// </summary>
public sealed class NoInfrastructureReferencesTests
{
    /// <summary>Packages that exist to talk to something outside this process.</summary>
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Npgsql",
        "Testcontainers",
        "Docker.DotNet",
        "WireMock",
        "Microsoft.AspNetCore.Mvc.Testing",
        "Microsoft.EntityFrameworkCore.Relational",
    ];

    [Fact]
    public void UnitAssembly_ReferencesNoDriverThatCouldReachOutsideTheProcess()
    {
        var referenced = typeof(NoInfrastructureReferencesTests).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToList();

        var offenders = referenced
            .Where(name => ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"The unit project references {string.Join(", ", offenders)}. Infrastructure belongs to " +
            "RecallRadar.Integration; a unit test is fully mocked (Article V).");
    }

    [Fact]
    public void EveryTestIsCoveredByTheTimingBackstop()
    {
        var isAssemblyAttributeApplied = typeof(NoInfrastructureReferencesTests).Assembly
            .GetCustomAttributes<UnitTestBudgetAttribute>()
            .Any();

        Assert.True(isAssemblyAttributeApplied, "The assembly-wide timing backstop is not applied.");
        Assert.True(UnitTestBudgetAttribute.IoCeilingMilliseconds > UnitTestBudgetAttribute.BudgetMilliseconds);
    }
}
