// Checks the request rules that guard every search, whether it arrives over HTTP or from the eval.
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Unit.Search;

public sealed class SearchRequestTests
{
    private const int VehicleId = 1;
    private const string ValidQuery = "exhaust odor in the cabin";

    [Fact]
    public void TryCreate_AcceptsAValidRequestAndNormalisesTheComponent()
    {
        var wasCreated = SearchRequest.TryCreate(
            VehicleId, $"  {ValidQuery}  ", "sparse", " structure ", null, null, 5, out var request, out var problem);

        Assert.True(wasCreated);
        Assert.Null(problem);
        Assert.Equal(ValidQuery, request!.Query);
        Assert.Equal("STRUCTURE", request.Component);
        Assert.Equal(RetrievalMode.Sparse, request.Mode);
        Assert.Equal(5, request.Limit);
    }

    [Fact]
    public void TryCreate_TreatsAnAbsentModeAsHybridAndAnAbsentLimitAsTheDefault()
    {
        SearchRequest.TryCreate(VehicleId, ValidQuery, null, null, null, null, null, out var request, out _);

        Assert.Equal(SearchRequest.DefaultMode, request!.Mode);
        Assert.Equal(RetrievalMode.Hybrid, request.Mode);
        Assert.Equal(SearchRequest.DefaultLimit, request.Limit);
        Assert.Null(request.Component);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryCreate_RejectsAQueryShorterThanTheMinimum(string? query)
    {
        var wasCreated = SearchRequest.TryCreate(VehicleId, query, "sparse", null, null, null, null, out _, out var problem);

        Assert.False(wasCreated);
        Assert.Contains(SearchRequest.MinimumQueryLength.ToString(), problem);
    }

    [Fact]
    public void TryCreate_RejectsAQueryLongerThanTheMaximum()
    {
        var tooLong = new string('x', SearchRequest.MaximumQueryLength + 1);

        var wasCreated = SearchRequest.TryCreate(VehicleId, tooLong, "sparse", null, null, null, null, out _, out var problem);

        Assert.False(wasCreated);
        Assert.Contains(SearchRequest.MaximumQueryLength.ToString(), problem);
    }

    [Fact]
    public void TryCreate_RejectsAModeItDoesNotKnow()
    {
        var wasCreated = SearchRequest.TryCreate(VehicleId, ValidQuery, "magic", null, null, null, null, out _, out var problem);

        Assert.False(wasCreated);
        Assert.Contains("magic", problem);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SearchRequest.MaximumLimit + 1)]
    public void TryCreate_RejectsALimitOutsideTheAllowedRange(int limit)
    {
        var wasCreated = SearchRequest.TryCreate(VehicleId, ValidQuery, "sparse", null, null, null, limit, out _, out var problem);

        Assert.False(wasCreated);
        Assert.Contains(SearchRequest.MaximumLimit.ToString(), problem);
    }

    [Fact]
    public void TryCreate_RejectsADateRangeThatHoldsNoDates()
    {
        var wasCreated = SearchRequest.TryCreate(
            VehicleId, ValidQuery, "sparse", null, new DateOnly(2020, 1, 2), new DateOnly(2020, 1, 1), null, out _, out var problem);

        Assert.False(wasCreated);
        Assert.Contains("filedFrom", problem);
    }

    [Fact]
    public void CandidateWindow_IsWiderThanTheLargestPageSoFusionHasSomethingToFuse()
    {
        Assert.True(SearchRequest.CandidateWindow >= SearchRequest.MaximumLimit);
    }
}
