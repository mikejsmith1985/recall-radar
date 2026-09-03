// Explains why a hit ranked where it did: its position under each method and the fused score.
namespace RecallRadar.Domain.Retrieval;

/// <summary>
/// Part of the API contract, not a debug extra (FR-007). A null rank means the method did not
/// return the item at all within its candidate window.
/// </summary>
/// <param name="DenseRank">One-based position in the embedding-distance ordering, or null.</param>
/// <param name="SparseRank">One-based position in the keyword ordering, or null.</param>
/// <param name="FusedScore">Reciprocal-rank-fusion score; higher is better.</param>
public sealed record RankExplanation(int? DenseRank, int? SparseRank, double FusedScore)
{
    /// <summary>True when both methods found the item, which is the strongest retrieval signal.</summary>
    public bool WasFoundByBoth => DenseRank is not null && SparseRank is not null;

    /// <summary>A single-method explanation, used when only dense or only sparse retrieval ran.</summary>
    public static RankExplanation SingleMethod(RetrievalMode mode, int rank, double score) => mode switch
    {
        RetrievalMode.Dense => new RankExplanation(rank, null, score),
        RetrievalMode.Sparse => new RankExplanation(null, rank, score),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Hybrid ranks come from fusion, not from a single method."),
    };
}
