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

    /// <summary>Initializes a new instance with a reason and inner cause.</summary>
    /// <param name="reason">Why the argument is invalid.</param>
    /// <param name="inner">Serialization cause.</param>
    public EdgeInvalidArgumentException(string reason, Exception? inner)
        : base(reason, inner)
    {
    }
}
