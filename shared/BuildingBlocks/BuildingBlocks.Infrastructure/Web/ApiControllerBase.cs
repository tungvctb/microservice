using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Infrastructure.Web;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private IDispatcher? _dispatcher;

    protected IDispatcher Dispatcher =>
        _dispatcher ??= HttpContext.RequestServices.GetRequiredService<IDispatcher>();

    protected string? CorrelationId => HttpContext.Items[CorrelationMiddleware.HeaderName] as string;

    /// <summary>Quy đổi Result nghiệp vụ sang HTTP status phù hợp.</summary>
    protected IActionResult ToResponse<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
            return StatusCode(successStatusCode, ApiResponse<T>.Ok(result.Value, CorrelationId));

        return Problem(result.Error);
    }

    protected IActionResult ToResponse(Result result, int successStatusCode = StatusCodes.Status204NoContent)
    {
        if (result.IsSuccess)
            return successStatusCode == StatusCodes.Status204NoContent
                ? NoContent()
                : StatusCode(successStatusCode, ApiResponse<object>.Ok(new { }, CorrelationId));

        return Problem(result.Error);
    }

    protected IActionResult CreatedResponse<T>(Result<T> result, string actionName, object routeValues)
    {
        if (result.IsFailure) return Problem(result.Error);
        return CreatedAtAction(actionName, routeValues, ApiResponse<T>.Ok(result.Value, CorrelationId));
    }

    private IActionResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var details = error.Type == ErrorType.Validation
            ? error.Message.Split(" | ", StringSplitOptions.RemoveEmptyEntries)
            : null;

        return StatusCode(status,
            ApiResponse<object>.Fail(new ApiError(error.Code, error.Message, details), CorrelationId));
    }
}
