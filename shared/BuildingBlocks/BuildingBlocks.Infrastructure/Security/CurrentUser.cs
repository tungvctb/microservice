using System.Security.Claims;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Infrastructure.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub"),
            out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue("email");

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? CorrelationId => accessor.HttpContext?.Items[CorrelationMiddleware.HeaderName] as string;
}
