using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers;

/// <summary>Liveness probe used by container orchestration (Docker Compose healthcheck) and uptime monitors.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    /// <summary>Returns 200 with the API assembly version when the process is responsive.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse(
        Status: "ok",
        Version: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0"));
}

/// <summary>Liveness probe payload.</summary>
public sealed record HealthResponse(string Status, string Version);
