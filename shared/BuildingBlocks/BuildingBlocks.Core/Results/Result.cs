namespace BuildingBlocks.Core.Results;

/// <summary>Kết quả thao tác — thay cho việc ném exception cho lỗi nghiệp vụ.</summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException("Result thành công không được kèm Error.");
        if (!isSuccess && error == Error.None) throw new InvalidOperationException("Result thất bại phải kèm Error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Không thể đọc Value của Result thất bại.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
