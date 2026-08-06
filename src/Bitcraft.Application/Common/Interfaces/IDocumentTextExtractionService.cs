using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Common.Interfaces;

public interface IDocumentTextExtractionService
{
    Task<Result<DocumentTextExtractionResult>> ExtractAsync(
        DocumentTextExtractionRequest request,
        CancellationToken cancellationToken = default);
}
