using Bitcraft.Api.Contracts.Auth;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IIdentityService identityService) : ControllerBase
{
    /// <summary>
    /// Registers a new user in the system.
    /// </summary>
    /// <param name="request">The registration details.</param>
    /// <returns>A successful authentication response or validation errors.</returns>
    [HttpPost("register")]
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
            Email = result.Email
        });
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <returns>A valid JWT for subsequent API calls.</returns>
    [HttpPost("login")]
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
            Email = result.Email
        });
    }
}
