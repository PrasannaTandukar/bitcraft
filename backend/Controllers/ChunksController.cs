using backend.Contracts.Chunks;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChunksController(ChunkService chunkService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DocumentChunk>> Create(CreateChunkRequest request, CancellationToken cancellationToken)
    {
        var result = await chunkService.AddChunkAsync(
            request.DocumentId,
            request.ChunkIndex,
            request.Content,
            request.Embedding,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<List<DocumentChunk>>> Search(SearchChunksRequest request, CancellationToken cancellationToken)
    {
        var results = await chunkService.SearchNearestAsync(
            request.QueryEmbedding,
            request.TopK,
            cancellationToken);

        return Ok(results);
    }
}