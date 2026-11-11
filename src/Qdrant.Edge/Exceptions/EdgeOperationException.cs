namespace Qdrant.Edge;

/// <summary>Thrown when the native engine reports an operational failure.</summary>
/// <param name="reason">Engine-provided failure reason.</param>
/// <param name="inner">Inner exception.</param>
public sealed class EdgeOperationException(string reason, Exception? inner = null) : EdgeException(reason, inner);
