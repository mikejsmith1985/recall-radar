// A grounded answer as returned to the user, with the citation accounting that proves it was checked.
namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// Persisted record of one question and its verified answer. The two citation counts are the
/// audit trail: dropped citations are counted, never hidden (Article X).
/// </summary>
public sealed class Answer
{
    public long Id { get; private set; }
    public int VehicleId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string AnswerJson { get; private set; } = string.Empty;
    public int VerifiedCitationCount { get; private set; }
    public int DroppedCitationCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>An answer is grounded only when at least one citation survived verification.</summary>
    public bool IsGrounded => VerifiedCitationCount > 0;

    private Answer() { }

    /// <summary>Creates an answer record with its citation accounting.</summary>
    public static Answer Create(
        int vehicleId, string question, string answerJson, int verifiedCitationCount, int droppedCitationCount, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(answerJson);
        ArgumentOutOfRangeException.ThrowIfNegative(verifiedCitationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(droppedCitationCount);

        return new Answer
        {
            VehicleId = vehicleId,
            Question = question.Trim(),
            AnswerJson = answerJson,
            VerifiedCitationCount = verifiedCitationCount,
            DroppedCitationCount = droppedCitationCount,
            CreatedAt = createdAt,
        };
    }
}
