namespace Bitcraft.Application.Common.Models;

public enum EmbeddingProviderFailureKind
{
    ServiceUnavailable,
    RequestFailed,
    ResponseInvalid
}

public sealed class EmbeddingProviderException(
    EmbeddingProviderFailureKind kind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public EmbeddingProviderFailureKind Kind { get; } = kind;
}
