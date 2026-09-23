using System.Text.Json;
using BuildingBlocks.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BuildingBlocks.Infrastructure.Web;

/// <summary>Biến exception chưa bắt thành ApiResponse thống nhất; không lộ stack trace ra ngoài.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationMiddleware.HeaderName] as string;
            logger.LogError(ex, "Request {Method} {Path} lỗi (correlation {CorrelationId})",
                context.Request.Method, context.Request.Path, correlationId);

            var (status, error) = Map(ex);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(ApiResponse<object>.Fail(error, correlationId), JsonOptions));
        }
    }

    private static (int Status, ApiError Error) Map(Exception ex) => ex switch
    {
        ConcurrencyException e => (StatusCodes.Status409Conflict, new ApiError(e.Code, e.Message)),
        DomainException e => (StatusCodes.Status400BadRequest, new ApiError(e.Code, e.Message)),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,
            new ApiError("auth.unauthorized", "Bạn chưa được xác thực.")),
        PostgresException { SqlState: "23505" } => (StatusCodes.Status409Conflict,
            new ApiError("db.duplicate", "Dữ liệu đã tồn tại.")),
        TaskCanceledException or OperationCanceledException => (StatusCodesExtra.Status499ClientClosedRequest,
            new ApiError("request.cancelled", "Request bị hủy.")),
        _ => (StatusCodes.Status500InternalServerError,
            new ApiError("server.error", "Đã có lỗi xảy ra. Vui lòng thử lại."))
    };
}

internal static class StatusCodesExtra
{
    public const int Status499ClientClosedRequest = 499;
}
