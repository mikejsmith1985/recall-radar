// Maps a retrieval scope onto the stored record kinds it admits.
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Retrieval.Search;

/// <summary>
/// The record kinds each <see cref="RetrievalScope"/> allows. Kept beside the persistence types
/// rather than in the domain, because <see cref="SourceKind"/> is what the database stores.
/// </summary>
public static class ScopedKinds
{
    private static readonly SourceKind[] EveryKind = Enum.GetValues<SourceKind>();

    /// <summary>Recalls and investigations: NHTSA's own record of a defect, as opposed to a report of one.</summary>
    private static readonly SourceKind[] Campaign = [SourceKind.Recall, SourceKind.Investigation];

    /// <summary>The kinds a scope admits, in a stable order.</summary>
    public static IReadOnlyList<SourceKind> Of(RetrievalScope scope) => scope switch
    {
        RetrievalScope.Campaigns => Campaign,
        _ => EveryKind,
    };
}
