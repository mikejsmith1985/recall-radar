// Scores every retrieval mode against the derived ground truth and records the result.
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using RecallRadar.Domain.Evaluation;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Retrieval.Evaluation;

/// <summary>One mode's score in one pool, or the reason it could not be scored.</summary>
/// <param name="Mode">The retrieval mode, lower-cased for the wire.</param>
/// <param name="Metrics">The numbers, or null when the mode was skipped.</param>
/// <param name="SkippedReason">Why the mode was skipped, or null when it ran.</param>
/// <param name="Scope">The pool the mode was measured within.</param>
[method: JsonConstructor]
public sealed record ModeResult(
    string Mode, RetrievalMetricsSummary? Metrics, string? SkippedReason, RetrievalScope Scope = RetrievalScope.All)
{
    [JsonIgnore]
    public bool WasScored => Metrics is not null;

    /// <summary>
    /// The key this result appears under. The wider pool keeps its bare mode name so an existing
    /// reader still finds it; the campaign pool is prefixed rather than nested, because nesting is
    /// what made a client render nothing when it forgot to unwrap. Serialised, not ignored: the
    /// results file lists one entry per mode per pool, and the bare mode name repeats across pools.
    /// </summary>
    public string Key => Scope == RetrievalScope.All
        ? Mode
        : Scope.ToString().ToLowerInvariant() + char.ToUpperInvariant(Mode[0]) + Mode[1..];
}

/// <summary>Everything one evaluation run measured.</summary>
public sealed record EvaluationResult(
    DateTimeOffset RanAt, int CaseCount, IReadOnlyList<ModeResult> Modes);

/// <summary>
/// Runs the same ground-truth cases through each retrieval mode and averages the metrics.
/// </summary>
/// <remarks>
/// Every mode sees identical cases in identical order, so the comparison between them is the only
/// thing that varies. A mode that cannot run, because it needs embeddings that do not exist, is
/// recorded as skipped with the reason rather than scored as zero: zero would read as "hybrid is
/// bad" when it means "hybrid was not tried".
/// </remarks>
public sealed class EvaluationRunner(
    GroundTruthBuilder groundTruth,
    HybridSearchService search,
    RecallRadarDbContext database,
    TimeProvider clock)
{
    /// <summary>How deep each ranking goes. Must cover the widest recall cutoff.</summary>
    public const int RankingDepth = RetrievalMetrics.RecallCutoffTen;

    private static readonly JsonSerializerOptions MetricsJsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>Runs the evaluation and stores it, returning what was measured.</summary>
    public async Task<EvaluationResult> RunAsync(int? vehicleId, CancellationToken cancellationToken)
    {
        var cases = await groundTruth.BuildAsync(vehicleId, cancellationToken);
        var modes = new List<ModeResult>();

        // Both pools, same cases, same order. The comparison between them is the measurement:
        // it says how much of the failure was ranking and how much was one kind of record
        // being outnumbered by another.
        foreach (var scope in new[] { RetrievalScope.All, RetrievalScope.Campaigns })
        {
            foreach (var mode in new[] { RetrievalMode.Sparse, RetrievalMode.Dense, RetrievalMode.Hybrid })
            {
                modes.Add(await ScoreModeAsync(mode, scope, cases, cancellationToken));
            }
        }

        var result = new EvaluationResult(clock.GetUtcNow(), cases.Count, modes);
        await StoreAsync(vehicleId, result, cancellationToken);
        return result;
    }

    /// <summary>Scores one mode over every case, or reports why it could not run.</summary>
    private async Task<ModeResult> ScoreModeAsync(
        RetrievalMode mode,
        RetrievalScope scope,
        IReadOnlyList<GroundTruthCase> cases,
        CancellationToken cancellationToken)
    {
        var name = mode.ToString().ToLowerInvariant();
        if (cases.Count == 0)
        {
            return new ModeResult(name, null, "No ground-truth cases exist yet. Load records first.", scope);
        }

        var rankings = new Dictionary<string, IReadOnlyList<long>>(StringComparer.Ordinal);
        foreach (var groundTruthCase in cases)
        {
            try
            {
                rankings[groundTruthCase.CaseId] = await RankAsync(mode, scope, groundTruthCase, cancellationToken);
            }
            catch (Exception failure) when (failure is EmbeddingsUnavailableException or EmbeddingProviderUnavailableException)
            {
                return new ModeResult(name, null, failure.Message, scope);
            }
        }

        return new ModeResult(name, RetrievalMetrics.Compute(cases, rankings), null, scope);
    }

    /// <summary>Retrieves for one case and returns the document ids in rank order.</summary>
    private async Task<IReadOnlyList<long>> RankAsync(
        RetrievalMode mode,
        RetrievalScope scope,
        GroundTruthCase groundTruthCase,
        CancellationToken cancellationToken)
    {
        var vehicleId = await FindVehicleForAsync(groundTruthCase, cancellationToken);
        if (!SearchRequest.TryCreate(
            vehicleId, groundTruthCase.QueryText, mode.ToString(), null, null, null, RankingDepth,
            scope.ToString(), out var request, out _))
        {
            return [];
        }

        var hits = await search.SearchAsync(request!, cancellationToken);
        return RankedHit.DistinctDocumentOrder(
            hits.Select(hit => new RankedHit(hit.DocumentId, hit.ChunkId, hit.Explanation)));
    }

    /// <summary>
    /// The vehicle a case belongs to, found from one of its relevant documents. Cases never span
    /// vehicles, so any relevant document answers this.
    /// </summary>
    private async Task<int> FindVehicleForAsync(GroundTruthCase groundTruthCase, CancellationToken cancellationToken)
    {
        var anyRelevantId = groundTruthCase.RelevantDocumentIds.First();
        var vehicleId = await database.SourceDocuments.AsNoTracking()
            .Where(document => document.Id == anyRelevantId)
            .Select(document => (int?)document.VehicleId)
            .FirstOrDefaultAsync(cancellationToken);
        return vehicleId ?? 0;
    }

    private async Task StoreAsync(int? vehicleId, EvaluationResult result, CancellationToken cancellationToken)
    {
        database.EvaluationRuns.Add(EvaluationRun.Create(
            vehicleId, result.RanAt, result.CaseCount, Serialise(result)));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Serialises the modes for storage and for the endpoint, in one stable shape.</summary>
    /// <remarks>
    /// Each mode maps straight to its numbers, or to null when it did not run, and the reasons live
    /// beside them under <c>skipped</c>. A wrapper object per mode would make every reader unwrap
    /// before it could read a number, and a client that forgot would silently render nothing.
    /// </remarks>
    public static string Serialise(EvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var mode in result.Modes)
        {
            payload[mode.Key] = mode.Metrics is { } metrics
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["recallAt5"] = metrics.RecallAt5,
                    ["recallAt10"] = metrics.RecallAt10,
                    ["mrr"] = metrics.MeanReciprocalRank,
                    ["scoredCaseCount"] = metrics.ScoredCaseCount,
                    ["skippedCaseCount"] = metrics.SkippedCaseCount,
                }
                : null;
        }

        var skipped = result.Modes
            .Where(mode => mode.SkippedReason is not null)
            .ToDictionary(mode => mode.Key, mode => mode.SkippedReason!, StringComparer.Ordinal);
        if (skipped.Count > 0)
        {
            payload["skipped"] = skipped;
        }

        return JsonSerializer.Serialize(payload, MetricsJsonOptions);
    }
}
