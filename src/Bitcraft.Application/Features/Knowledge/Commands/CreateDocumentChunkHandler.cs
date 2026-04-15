using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Domain.Knowledge;

namespace Bitcraft.Application.Features.Knowledge.Commands;

public class CreateDocumentChunkHandler(IKnowledgeRepository knowledgeRepository)
{
    public async Task<DocumentChunk> HandleAsync(CreateDocumentChunkCommand command, CancellationToken cancellationToken = default)
    {
        return await knowledgeRepository.AddChunkAsync(
            command.DocumentId,
            command.ChunkIndex,
            command.Content,
            command.Embedding,
            cancellationToken);
    }
}
