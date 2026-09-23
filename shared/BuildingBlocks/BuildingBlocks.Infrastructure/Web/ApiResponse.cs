namespace BuildingBlocks.Infrastructure.Web;

/// <summary>Envelope thống nhất cho mọi endpoint REST — FE chỉ cần xử lý một dạng payload.</summary>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string? CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? correlationId = null) =>
        new() { Success = true, Data = data, CorrelationId = correlationId };

    public static ApiResponse<T> Fail(ApiError error, string? correlationId = null) =>
        new() { Success = false, Error = error, CorrelationId = correlationId };
}

public sealed record ApiError(string Code, string Message, IReadOnlyList<string>? Details = null);
