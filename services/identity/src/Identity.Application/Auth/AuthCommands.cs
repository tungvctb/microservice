using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Identity.Application.Common;
using Identity.Domain.Entities;
using Identity.Domain.Errors;
using Identity.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth;

public sealed record RegisterCommand(string Email, string Password, string FullName, string? Phone)
    : ICommand<AuthResultDto>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).Matches(@"^[0-9+\-\s]{8,15}$").When(x => !string.IsNullOrEmpty(x.Phone));
    }
}

internal sealed class RegisterHandler(
    IUserRepository users, IPasswordHasher hasher, ITokenGenerator tokens,
    IUnitOfWork unitOfWork, ILogger<RegisterHandler> logger)
    : ICommandHandler<RegisterCommand, AuthResultDto>
{
    public async Task<Result<AuthResultDto>> Handle(RegisterCommand command, CancellationToken ct)
    {
        if (await users.EmailExistsAsync(command.Email, ct))
            return Result.Failure<AuthResultDto>(IdentityErrors.EmailTaken(command.Email));

        var user = User.Create(command.Email, hasher.Hash(command.Password),
            command.FullName, command.Phone, UserRoles.Customer);

        users.Add(user);

        var refreshToken = tokens.GenerateRefreshToken();
        user.IssueRefreshToken(refreshToken, DateTime.UtcNow.AddDays(tokens.RefreshTokenDays));

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Đã đăng ký tài khoản mới {Email}", user.Email);

        return Result.Success(new AuthResultDto(
            tokens.GenerateAccessToken(user), refreshToken,
            tokens.AccessTokenMinutes * 60, user.ToDto()));
    }
}

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResultDto>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

internal sealed class LoginHandler(
    IUserRepository users, IPasswordHasher hasher, ITokenGenerator tokens, IUnitOfWork unitOfWork)
    : ICommandHandler<LoginCommand, AuthResultDto>
{
    public async Task<Result<AuthResultDto>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(command.Email, ct);

        // Trả cùng một lỗi cho "không có user" và "sai mật khẩu" — tránh dò email tồn tại.
        if (user is null || !hasher.Verify(command.Password, user.PasswordHash))
            return Result.Failure<AuthResultDto>(IdentityErrors.InvalidCredentials);

        if (!user.IsActive)
            return Result.Failure<AuthResultDto>(IdentityErrors.AccountDisabled);

        var refreshToken = tokens.GenerateRefreshToken();
        user.IssueRefreshToken(refreshToken, DateTime.UtcNow.AddDays(tokens.RefreshTokenDays));
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new AuthResultDto(
            tokens.GenerateAccessToken(user), refreshToken,
            tokens.AccessTokenMinutes * 60, user.ToDto()));
    }
}

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResultDto>;

/// <summary>Xoay vòng refresh token: token cũ bị thu hồi ngay khi cấp token mới.</summary>
internal sealed class RefreshTokenHandler(
    IUserRepository users, ITokenGenerator tokens, IUnitOfWork unitOfWork)
    : ICommandHandler<RefreshTokenCommand, AuthResultDto>
{
    public async Task<Result<AuthResultDto>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var user = await users.GetByRefreshTokenAsync(command.RefreshToken, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<AuthResultDto>(IdentityErrors.InvalidRefreshToken);

        var existing = user.FindActiveRefreshToken(command.RefreshToken);
        if (existing is null)
            return Result.Failure<AuthResultDto>(IdentityErrors.InvalidRefreshToken);

        user.RevokeAllRefreshTokens("Xoay vòng token");

        var newRefreshToken = tokens.GenerateRefreshToken();
        user.IssueRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(tokens.RefreshTokenDays));
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new AuthResultDto(
            tokens.GenerateAccessToken(user), newRefreshToken,
            tokens.AccessTokenMinutes * 60, user.ToDto()));
    }
}

public sealed record LogoutCommand(Guid UserId) : ICommand<Unit>;

internal sealed class LogoutHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> Handle(LogoutCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure<Unit>(IdentityErrors.UserNotFound(command.UserId));

        user.RevokeAllRefreshTokens("Đăng xuất");
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(Unit.Value);
    }
}
