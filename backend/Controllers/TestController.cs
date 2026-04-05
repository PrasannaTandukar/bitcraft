using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IConfiguration _config;

    public TestController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("db-test")]
    public async Task<IActionResult> DbTest()
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");

        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("SELECT version();", conn);
            var result = await cmd.ExecuteScalarAsync();

            return Ok(new
            {
                success = true,
                message = "Database connection successful",
                databaseVersion = result?.ToString()
            });
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Database connection failed",
                detail: ex.Message
            );
        }
    }
}