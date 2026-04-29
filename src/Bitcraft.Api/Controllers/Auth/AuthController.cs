using Bitcraft.Api.Contracts.Auth;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IIdentityService identityService) : ControllerBase
{
    /// <summary>Registers a new Tutor Studio user and returns a JWT.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await identityService.RegisterAsync(request.Email, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return Ok(new AuthResponse
        {
            Token = result.Token,
            Email = result.Email,
            Roles = result.Roles
        });
    }

    /// <summary>Authenticates an existing Tutor Studio user and returns a JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await identityService.LoginAsync(request.Email, request.Password);

        if (!result.Succeeded)
        {
            return Unauthorized(result.Errors);
        }

        return Ok(new AuthResponse
        {
            Token = result.Token,
            Email = result.Email,
            Roles = result.Roles
        });
    }
}
