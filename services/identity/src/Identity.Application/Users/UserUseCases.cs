using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Security;
using BuildingBlocks.Core.Abstractions;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using FluentValidation;
using Identity.Application.Common;
using Identity.Domain.Errors;
using Identity.Domain.Repositories;

namespace Identity.Application.Users;

public sealed record GetCurrentUserQuery : IQuery<UserDto>;

internal sealed class GetCurrentUserHandler(IUserRepository users, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, UserDto>
{
    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Result.Failure<UserDto>(IdentityErrors.InvalidCredentials);

        var user = await users.GetByIdAsync(currentUser.UserId.Value, ct);
        return user is null
            ? Result.Failure<UserDto>(IdentityErrors.UserNotFound(currentUser.UserId.Value))
            : Result.Success(user.ToDto());
    }
}

public sealed record SearchUsersQuery(string? Search = null, string? Role = null,
    bool? IsActive = null, int Page = 1, int PageSize = 20) : IQuery<PagedResult<UserDto>>;

internal sealed class SearchUsersHandler(IUserRepository users)
    : IQueryHandler<SearchUsersQuery, PagedResult<UserDto>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(SearchUsersQuery query, CancellationToken ct)
    {
        var page = await users.SearchAsync(new UserFilter
        {
            Search = query.Search,
            Role = query.Role,
            IsActive = query.IsActive,
            Page = query.Page,
            PageSize = query.PageSize
        }, ct);

        return Result.Success(new PagedResult<UserDto>(
            page.Items.Select(u => u.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount));
    }
}

public sealed record UpdateProfileCommand(string FullName, string? Phone) : ICommand<UserDto>;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).Matches(@"^[0-9+\-\s]{8,15}$").When(x => !string.IsNullOrEmpty(x.Phone));
    }
}

internal sealed class UpdateProfileHandler(
    IUserRepository users, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProfileCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Result.Failure<UserDto>(IdentityErrors.InvalidCredentials);

        var user = await users.GetByIdAsync(currentUser.UserId.Value, ct);
        if (user is null) return Result.Failure<UserDto>(IdentityErrors.UserNotFound(currentUser.UserId.Value));

        user.UpdateProfile(command.FullName, command.Phone);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(user.ToDto());
    }
}

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Unit>;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]")
            .WithMessage("Mật khẩu mới phải có chữ hoa, chữ thường và chữ số, tối thiểu 8 ký tự.");
    }
}

internal sealed class ChangePasswordHandler(
    IUserRepository users, ICurrentUser currentUser, IPasswordHasher hasher, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangePasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Result.Failure<Unit>(IdentityErrors.InvalidCredentials);

        var user = await users.GetByIdAsync(currentUser.UserId.Value, ct);
        if (user is null) return Result.Failure<Unit>(IdentityErrors.UserNotFound(currentUser.UserId.Value));

        if (!hasher.Verify(command.CurrentPassword, user.PasswordHash))
            return Result.Failure<Unit>(IdentityErrors.WrongCurrentPassword);

        user.ChangePassword(hasher.Hash(command.NewPassword));
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(Unit.Value);
    }
}

public sealed record SetUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles) : ICommand<UserDto>;

internal sealed class SetUserRolesHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<SetUserRolesCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(SetUserRolesCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure<UserDto>(IdentityErrors.UserNotFound(command.UserId));

        user.SetRoles(command.Roles);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(user.ToDto());
    }
}

public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : ICommand<UserDto>;

internal sealed class SetUserActiveHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<SetUserActiveCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(SetUserActiveCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure<UserDto>(IdentityErrors.UserNotFound(command.UserId));

        user.SetActive(command.IsActive);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(user.ToDto());
    }
}
