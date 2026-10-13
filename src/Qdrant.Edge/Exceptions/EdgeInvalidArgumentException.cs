namespace Qdrant.Edge;

/// <summary>Thrown when an argument fails engine-parity validation.</summary>
public sealed class EdgeInvalidArgumentException : EdgeException
{
    /// <summary>Initializes a new instance with a reason.</summary>
    /// <param name="reason">Why the argument is invalid.</param>
    public EdgeInvalidArgumentException(string reason)
        : base(reason)
    {
    }
}
