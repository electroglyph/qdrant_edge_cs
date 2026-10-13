using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class ExpressionValidationTests
{
    [Fact]
    public void Constant_NonFiniteValue_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Constant(float.PositiveInfinity));
    }

    [Fact]
    public void Max_EmptyOperands_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => Expression.Max([]));
    }

    [Fact]
    public void Variable_AnyName_SucceedsWithoutThrow()
    {
        Expression expression = Expression.Variable("score");
        Assert.Equal("variable", expression.Kind);
    }

    [Fact]
    public void Combine_DeepChainBeyondCap_ThrowsInvalidArgument()
    {
        Expression current = Expression.Variable("x");
        Assert.Throws<EdgeInvalidArgumentException>(() =>
        {
            for (int i = 0; i < 80; i++)
            {
                current = Expression.Negate(current);
            }
        });
    }

    [Fact]
    public void Sum_TwoTerms_MeasuresThreeNodesDeepTwo()
    {
        Expression sum = Expression.Sum([Expression.Variable("a"), Expression.Constant(1f)]);
        SumExpression terms = Assert.IsType<SumExpression>(sum);
        Assert.Equal(2, terms.Terms.Count);
        Assert.Equal(3ul, sum.Size);
        Assert.Equal(2u, sum.Depth);
    }

    [Fact]
    public void Sum_TwoTerms_RoundTripsThroughJson()
    {
        Expression sum = Expression.Sum([Expression.Variable("a"), Expression.Constant(1f)]);
        string json = JsonSerializer.Serialize(sum);
        Expression? restored = JsonSerializer.Deserialize<Expression>(json);
        SumExpression terms = Assert.IsType<SumExpression>(restored);
        Assert.Equal(2, terms.Terms.Count);
        Assert.Equal(sum.Size, restored.Size);
        Assert.Equal(sum.Depth, restored.Depth);
    }

    [Fact]
    public void Div_WithFallback_PreservesOperands()
    {
        Expression div = Expression.Div(Expression.Variable("a"), Expression.Constant(2f), 0f);
        DivExpression parts = Assert.IsType<DivExpression>(div);
        Assert.Equal(Expression.Variable("a"), parts.Left);
        Assert.Equal(0f, parts.ByZeroDefault);
    }
}
