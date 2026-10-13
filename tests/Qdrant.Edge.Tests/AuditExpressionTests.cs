namespace Qdrant.Edge.Tests;

/// <summary>Formula size and depth are recomputed from the tree shape, so
/// forged counters cannot smuggle an oversized expression past validation.</summary>
public sealed class AuditExpressionTests
{
    [Fact]
    public void ExpressionValidate_ForgedShallowDepthOnDeepTree_ThrowsInvalidArgument()
    {
        Expression current = Expression.Variable("x");
        for (int i = 0; i < 70; i++)
        {
            current = Expression.Negate(current) with { Size = 1, Depth = 1 };
        }

        Assert.Throws<EdgeInvalidArgumentException>(current.Validate);
    }
}
