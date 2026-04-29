using Bitcraft.Api.Contracts;
using Bitcraft.Api.Contracts.Admin;
using Bitcraft.Api.Mapping;
using Bitcraft.Application.Common.Security;
using Bitcraft.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bitcraft.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/users/{userId:guid}/roles")]
[Produces("application/json")]
public class AdminUserRolesController(ISender sender) : ControllerBase
{
    /// <summary>Gets the roles currently assigned to a user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserRoleMembershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRoleMembershipResponse>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RoleManagementFeatures.GetUserRolesQuery(userId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Assigns a role to a user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserRoleMembershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRoleMembershipResponse>> Assign(
        Guid userId,
        [FromBody] AssignUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RoleManagementFeatures.AssignUserRoleCommand(userId, request.Role), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }

    /// <summary>Revokes a role from a user.</summary>
    [HttpDelete("{role}")]
    [ProducesResponseType(typeof(UserRoleMembershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRoleMembershipResponse>> Revoke(
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RoleManagementFeatures.RevokeUserRoleCommand(userId, role), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.ToResponse())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
