using System.Security.Claims;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Bitcraft.Infrastructure.Identity;

public class HttpContextCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId => UserIdOrNull
        ?? throw new UnauthorizedAccessException("Current user is not authenticated.");

    public Guid? UserIdOrNull
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue("sub");

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public bool IsInRole(string role)
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true && user.IsInRole(role);
    }
}
