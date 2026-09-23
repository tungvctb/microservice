using BuildingBlocks.Application.Security;
using BuildingBlocks.Infrastructure.Web;
using Identity.Application.Auth;
using Identity.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));

    /// <summary>Đổi refresh token lấy cặp token mới. Token cũ bị thu hồi ngay.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(command, ct));

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (currentUser.UserId is null) return Unauthorized();
        return ToResponse(await Dispatcher.Send(new LogoutCommand(currentUser.UserId.Value), ct));
    }
}
