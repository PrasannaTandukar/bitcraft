using Bitcraft.Api.Contracts.Knowledge;
using Bitcraft.Application.Features.Knowledge.Commands;
using Bitcraft.Application.Features.Knowledge.Queries;
using Bitcraft.Domain.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeController(CreateDocumentChunkHandler createDocumentChunkHandler, SearchKnowledgeHandler searchKnowledgeHandler) : ControllerBase
{
    [HttpPost("chunks")]
    public async Task<ActionResult<DocumentChunk>> CreateChunk(CreateChunkRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateDocumentChunkCommand(
            request.DocumentId,
            request.ChunkIndex,
            request.Content,
            request.Embedding);

        var result = await createDocumentChunkHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<List<DocumentChunk>>> Search(SearchKnowledgeRequest request, CancellationToken cancellationToken)
    {
        var query = new SearchKnowledgeQuery(request.QueryEmbedding, request.TopK);
        var results = await searchKnowledgeHandler.HandleAsync(query, cancellationToken);
        return Ok(results);
    }
}
