namespace Qdrant.Edge;

/// <summary>Thrown when an argument fails engine-parity validation.</summary>
/// <param name="reason">Why the argument is invalid.</param>
/// <param name="inner">Serialization cause.</param>
public sealed class EdgeInvalidArgumentException(string reason, Exception? inner = null) : EdgeException(reason, inner);
