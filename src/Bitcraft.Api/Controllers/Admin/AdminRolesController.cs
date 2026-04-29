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
[Route("api/admin/roles")]
[Produces("application/json")]
public class AdminRolesController(ISender sender) : ControllerBase
{
    /// <summary>Lists the roles available for Bitcraft authorization policies.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RoleManagementFeatures.ListRolesQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(role => new RoleResponse(role)).ToList())
            : TutoringMapping.ToActionResult(result.Error);
    }
}
