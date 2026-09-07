// Proves the NHTSA client folder is read-only: no mutating HTTP verb appears in its source or its API.
using System.Reflection;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Integration.Ingest;

/// <summary>
/// FR-002 in executable form. The source scan catches any future <c>PostAsync</c>; the reflection
/// check catches a public method whose name promises something other than reading.
/// </summary>
public sealed class NoMutatingVerbsTests
{
    private const string SolutionMarker = "RecallRadar.slnx";
    private const string ClientSuffix = "Client";
    private static readonly string[] ForbiddenTokens =
    [
        "HttpMethod.Post", "HttpMethod.Put", "HttpMethod.Delete", "HttpMethod.Patch",
        "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync", "SendAsync", "PostAsJsonAsync", "PutAsJsonAsync",
    ];
    // "Is" joins the list for predicates over a response body already in hand. The rule exists to
    // stop a method promising to change something at NHTSA; a boolean question about bytes we have
    // already been given cannot. Post/Put/Delete/Send remain forbidden by name and by source scan.
    private static readonly string[] AllowedPublicMethodPrefixes = ["Get", "Download", "Parse", "Build", "Add", "Is"];

    [Fact]
    public void NhtsaSourceFolder_ContainsNoMutatingHttpCall()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "src", "RecallRadar.Ingest", "Nhtsa");
        var sources = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);

        Assert.NotEmpty(sources);
        foreach (var source in sources)
        {
            var text = File.ReadAllText(source);
            foreach (var token in ForbiddenTokens)
            {
                Assert.False(text.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(source)} contains forbidden token '{token}'.");
            }
        }
    }

    [Fact]
    public void NhtsaClients_ExposeOnlyReadingMethods()
    {
        var clientTypes = typeof(NhtsaComplaintsClient).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(NhtsaComplaintsClient).Namespace && type.Name.EndsWith(ClientSuffix, StringComparison.Ordinal))
            .ToList();

        // Complaints, recalls, models, the flat file and vPIC. Asserted so a client added without
        // being considered here fails rather than slipping past the check below unnoticed.
        Assert.Equal(5, clientTypes.Count);

        foreach (var type in clientTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName);
            foreach (var method in methods)
            {
                Assert.True(
                    AllowedPublicMethodPrefixes.Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal)),
                    $"{type.Name}.{method.Name} does not read: only {string.Join("/", AllowedPublicMethodPrefixes)} methods belong in the NHTSA folder.");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionMarker)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"Could not find {SolutionMarker} above {AppContext.BaseDirectory}.");
    }
}
