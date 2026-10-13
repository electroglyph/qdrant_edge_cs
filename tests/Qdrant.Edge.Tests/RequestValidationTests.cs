using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class RequestValidationTests
{
    [Fact]
    public void SearchRequest_LimitAboveCap_ThrowsOnValidate()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(NamedVector.Dense([1f])),
            Limit = EdgeValidation.MaxResultCount + 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void MmrScoringQuery_LambdaOutOfRange_ThrowsOnValidate()
    {
        MmrScoringQuery mmr = new(NamedVector.Dense([1f]), 2f, 10, null);
        Assert.Throws<EdgeInvalidArgumentException>(mmr.Validate);
    }

    [Fact]
    public void GroupRequest_GroupSizeAboveCap_ThrowsOnValidate()
    {
        GroupRequest request = new()
        {
            Query = new QueryRequest { Limit = 10 },
            GroupBy = "city",
            Groups = 5,
            GroupSize = EdgeValidation.MaxResultCount + 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void FacetRequest_EmptyKey_ThrowsOnValidate()
    {
        FacetRequest request = new() { Key = string.Empty };
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void FacetResponse_ObjectShape_DeserializesHits()
    {
        FacetResponse? response = JsonSerializer.Deserialize<FacetResponse>(
            """{"Hits":[{"Value":"red","Count":3}]}""");
        Assert.NotNull(response);
        Assert.Equal(1, response.Count);
        Assert.Equal("red", response.Hits[0].Value);
        Assert.Equal(3ul, response.Hits[0].Count);
        Assert.Equal(response.Hits[0], response[0]);
    }
}
