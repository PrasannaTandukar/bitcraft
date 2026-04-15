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
    public async Task<ActionResult<DocumentChunk>> CreateChunk(CreateDocumentChunkCommand command, CancellationToken cancellationToken)
    {
        var result = await createDocumentChunkHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<List<DocumentChunk>>> Search(SearchKnowledgeQuery query, CancellationToken cancellationToken)
    {
        var results = await searchKnowledgeHandler.HandleAsync(query, cancellationToken);
        return Ok(results);
    }
}
