// Checks that a source record keeps its body verbatim, because citations are verified against it.
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class SourceDocumentTests
{
    private const string BodyWithOddWhitespace = "  The contact owns a 2013 Ford Explorer.\n\n  Exhaust  odor in cabin. ";

    [Fact]
    public void Create_StoresBodyExactlyAsReceived()
    {
        var document = SourceDocument.Create(
            SourceKind.Complaint, " 11760888 ", vehicleId: 1, component: " STRUCTURE ",
            filedOn: new DateOnly(2026, 8, 31), title: " Complaint ", body: BodyWithOddWhitespace, rawPayload: "{}");

        Assert.Equal(BodyWithOddWhitespace, document.Body);
        Assert.Equal("11760888", document.ExternalId);
        Assert.Equal("STRUCTURE", document.Component);
        Assert.Equal("Complaint", document.Title);
        Assert.Equal(SourceKind.Complaint, document.Kind);
    }

    [Fact]
    public void Create_RejectsRecordWithoutBodyBecauseItCouldNeverBeCited()
    {
        Assert.Throws<ArgumentException>(() =>
            SourceDocument.Create(SourceKind.Recall, "19V435000", 1, "SUSPENSION:REAR", null, "Recall", "   ", "{}"));
    }

    [Fact]
    public void Create_RejectsRecordWithoutExternalId()
    {
        Assert.Throws<ArgumentException>(() =>
            SourceDocument.Create(SourceKind.Investigation, "", 1, "ENGINE", null, "EA17-002", "Summary text", ""));
    }
}
