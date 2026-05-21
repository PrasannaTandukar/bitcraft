using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Bitcraft.Api.Controllers;

/// <summary>Liveness probe used by container orchestration (Docker Compose healthcheck) and uptime monitors.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
[Produces("application/json")]
public class HealthController(IHostEnvironment environment) : ControllerBase
{
    /// <summary>
    /// Returns 200 when the process is responsive. The assembly <c>Version</c>
    /// field is only populated in the Development environment to avoid leaking
    /// build metadata to anonymous callers in non-dev environments.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        var version = environment.IsDevelopment()
            ? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0"
            : null;
        return Ok(new HealthResponse(Status: "ok", Version: version));
    }
}

/// <summary>
/// Liveness probe payload. <see cref="Version"/> is only populated in the
/// Development environment; it is <see langword="null"/> in Staging/Production
/// to avoid disclosing build metadata to anonymous callers.
/// </summary>
public sealed record HealthResponse(string Status, string? Version);
