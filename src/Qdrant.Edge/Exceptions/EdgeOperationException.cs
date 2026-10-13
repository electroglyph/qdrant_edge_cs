namespace Qdrant.Edge;

/// <summary>Thrown when the native engine reports an operational failure.</summary>
public sealed class EdgeOperationException : EdgeException
{
    /// <summary>Initializes a new instance with a reason.</summary>
    /// <param name="reason">Engine-provided failure reason.</param>
    public EdgeOperationException(string reason)
        : base(reason)
    {
    }

    /// <summary>Initializes a new instance with a reason and inner exception.</summary>
    /// <param name="reason">Engine-provided failure reason.</param>
    /// <param name="inner">Inner exception.</param>
    public EdgeOperationException(string reason, Exception? inner)
        : base(reason, inner)
    {
    }
}
