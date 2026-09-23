using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Infrastructure.Web;
using Identity.Application.Common;
using Identity.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[Route("api/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController : ApiControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetCurrentUserQuery(), ct));

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));

    [HttpPost("me/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchUsersQuery query, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(query, ct));

    [HttpPut("{id:guid}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetRolesRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new SetUserRolesCommand(id, request.Roles), ct));

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new SetUserActiveCommand(id, request.IsActive), ct));
}

public sealed record SetRolesRequest(IReadOnlyList<string> Roles);
public sealed record SetActiveRequest(bool IsActive);
