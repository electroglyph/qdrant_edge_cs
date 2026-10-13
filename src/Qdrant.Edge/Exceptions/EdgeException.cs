namespace Qdrant.Edge;

/// <summary>
/// Base exception for all Qdrant Edge failures. Carries the engine's
/// reason string verbatim; never repurposed as a <c>Message</c> field name
///ollision workaround is documented on <see cref="Reason"/>.
/// </summary>
public class EdgeException : Exception
{
    /// <summary>Engine-provided failure reason.</summary>
    public string Reason { get; }

    /// <summary>Initializes a new instance with a reason.</summary>
    /// <param name="reason">Engine-provided failure reason.</param>
    protected EdgeException(string reason)
        : base(reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason, nameof(reason));
        Reason = reason;
    }

    /// <summary>Initializes a new instance with a reason and inner exception.</summary>
    /// <param name="reason">Engine-provided failure reason.</param>
    /// <param name="inner">Inner exception.</param>
    protected EdgeException(string reason, Exception? inner)
        : base(reason, inner)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason, nameof(reason));
        Reason = reason;
    }
}
