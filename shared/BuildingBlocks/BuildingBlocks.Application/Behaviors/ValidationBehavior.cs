using BuildingBlocks.Core.Results;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>Chạy toàn bộ IValidator đăng ký cho message; trả Error kiểu Validation nếu có lỗi.</summary>
public static class ValidationBehavior
{
    public static async Task<Error?> ValidateAsync<TMessage>(TMessage message, IServiceProvider sp, CancellationToken ct)
    {
        var validators = sp.GetServices<IValidator<TMessage>>().ToArray();
        if (validators.Length == 0) return null;

        var context = new ValidationContext<TMessage>(message!);
        var failures = new List<string>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, ct);
            failures.AddRange(result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        }

        return failures.Count == 0
            ? null
            : Error.Validation("validation.failed", string.Join(" | ", failures));
    }
}
