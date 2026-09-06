// A validated search request: what to look for, in which vehicle, under which retrieval method.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Retrieval.Search;

/// <summary>
/// Everything the search service needs, already checked. Validation lives here rather than in the
/// endpoint so the same rules apply to the evaluation harness, which does not go through HTTP.
/// </summary>
/// <param name="VehicleId">The vehicle whose records may be returned. Never crosses vehicles.</param>
/// <param name="Query">The owner's own words.</param>
/// <param name="Mode">Dense, sparse, or the fusion of both.</param>
/// <param name="Component">Optional exact NHTSA component filter.</param>
/// <param name="FiledFrom">Optional earliest filing date, inclusive.</param>
/// <param name="FiledTo">Optional latest filing date, inclusive.</param>
/// <param name="Limit">How many hits to return.</param>
/// <param name="Scope">Which pool of records may be returned.</param>
public sealed record SearchRequest(
    int VehicleId,
    string Query,
    RetrievalMode Mode,
    string? Component,
    DateOnly? FiledFrom,
    DateOnly? FiledTo,
    int Limit,
    RetrievalScope Scope)
{
    public const int MinimumQueryLength = 2;
    public const int MaximumQueryLength = 500;
    public const int DefaultLimit = 10;
    public const int MaximumLimit = 50;

    /// <summary>What an unnamed mode means, per the HTTP contract.</summary>
    public const RetrievalMode DefaultMode = RetrievalMode.Hybrid;

    /// <summary>What an unnamed scope means: every record, which is what search did before pools existed.</summary>
    public const RetrievalScope DefaultScope = RetrievalScope.All;

    /// <summary>How many candidates each method contributes before fusion. Fifty per the research note.</summary>
    public const int CandidateWindow = 50;

    /// <summary>
    /// Builds a request, returning the first problem instead of throwing. The endpoint turns a
    /// problem into a 400 and the evaluation harness treats it as a skipped case.
    /// </summary>
    public static bool TryCreate(
        int vehicleId,
        string? query,
        string? mode,
        string? component,
        DateOnly? filedFrom,
        DateOnly? filedTo,
        int? limit,
        out SearchRequest? request,
        out string? problem) =>
        TryCreate(vehicleId, query, mode, component, filedFrom, filedTo, limit, null, out request, out problem);

    /// <summary>
    /// Builds a request for a named pool of records. Separate from the seven-argument overload so
    /// that every existing caller keeps searching everything, which is what it used to do.
    /// </summary>
    public static bool TryCreate(
        int vehicleId,
        string? query,
        string? mode,
        string? component,
        DateOnly? filedFrom,
        DateOnly? filedTo,
        int? limit,
        string? scope,
        out SearchRequest? request,
        out string? problem)
    {
        request = null;
        problem = FindProblem(query, mode, filedFrom, filedTo, limit, scope);
        if (problem is not null)
        {
            return false;
        }

        request = new SearchRequest(
            vehicleId,
            query!.Trim(),
            ResolveMode(mode)!.Value,
            string.IsNullOrWhiteSpace(component) ? null : component.Trim().ToUpperInvariant(),
            filedFrom,
            filedTo,
            limit ?? DefaultLimit,
            ResolveScope(scope)!.Value);
        return true;
    }

    /// <summary>An absent mode means hybrid, the documented default; a named mode must be one we know.</summary>
    private static RetrievalMode? ResolveMode(string? mode) =>
        string.IsNullOrWhiteSpace(mode) ? DefaultMode : RetrievalModes.TryParse(mode);

    /// <summary>An absent scope means every record; a named scope must be one we know.</summary>
    private static RetrievalScope? ResolveScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? DefaultScope : RetrievalScopes.TryParse(scope);

    private static string? FindProblem(
        string? query, string? mode, DateOnly? filedFrom, DateOnly? filedTo, int? limit, string? scope)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length is < MinimumQueryLength or > MaximumQueryLength)
        {
            return $"The query must be between {MinimumQueryLength} and {MaximumQueryLength} characters.";
        }

        if (ResolveMode(mode) is null)
        {
            return $"Unknown mode '{mode}'. Use dense, sparse, or hybrid.";
        }

        if (ResolveScope(scope) is null)
        {
            return $"Unknown scope '{scope}'. Use all or campaigns.";
        }

        if (limit is < 1 or > MaximumLimit)
        {
            return $"The limit must be between 1 and {MaximumLimit}.";
        }

        if (filedFrom is { } from && filedTo is { } to && from > to)
        {
            return "filedFrom is later than filedTo, so the range holds no dates.";
        }

        return null;
    }
}
