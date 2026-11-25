namespace ShiftUnlocker.Core.Common;

public class Result
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? Error => ErrorMessage;

    protected Result(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public static Result Ok() => new Result(true, null);
    public static Result Fail(string message) => new Result(false, message);
}

public class Result<T> : Result
{
    public T? Value { get; }

    protected Result(bool success, T? value, string? errorMessage)
        : base(success, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new Result<T>(true, value, null);
    public new static Result<T> Fail(string message) => new Result<T>(false, default, message);
}
