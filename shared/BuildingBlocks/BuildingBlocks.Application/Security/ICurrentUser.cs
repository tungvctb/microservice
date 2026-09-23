namespace BuildingBlocks.Application.Security;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
    string? CorrelationId { get; }
}
