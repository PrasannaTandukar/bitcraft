using Bitcraft.Application.Features.Chunks.CreateChunk;
using Bitcraft.Application.Features.Chunks.SearchChunks;
using Bitcraft.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChunksController(CreateChunkHandler createChunkHandler, SearchChunksHandler searchChunksHandler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DocumentChunk>> Create(CreateChunkCommand command, CancellationToken cancellationToken)
    {
        var result = await createChunkHandler.HandleAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<List<DocumentChunk>>> Search(SearchChunksQuery query, CancellationToken cancellationToken)
    {
        var results = await searchChunksHandler.HandleAsync(query, cancellationToken);
        return Ok(results);
    }
}
