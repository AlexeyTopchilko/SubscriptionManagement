namespace SubscriptionManager.Api.Services;

public class ServiceResult
{
    public bool IsSuccess { get; protected init; }
    public string? ErrorMessage { get; protected init; }
    public ServiceResultType ResultType { get; protected init; }

    public static ServiceResult Success() =>
        new() { IsSuccess = true, ResultType = ServiceResultType.Ok };

    public static ServiceResult NotFound(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ResultType = ServiceResultType.NotFound };

    public static ServiceResult Conflict(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ResultType = ServiceResultType.Conflict };

    public static ServiceResult BadRequest(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ResultType = ServiceResultType.BadRequest };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Success(T data) =>
        new() { IsSuccess = true, Data = data, ResultType = ServiceResultType.Ok };

    public new static ServiceResult<T> NotFound(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ResultType = ServiceResultType.NotFound };

    public new static ServiceResult<T> Conflict(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ResultType = ServiceResultType.Conflict };

    public new static ServiceResult<T> BadRequest(string message) =>
        new() { IsSuccess = false, ErrorMessage = message, ResultType = ServiceResultType.BadRequest };
}

public enum ServiceResultType
{
    Ok,
    NotFound,
    Conflict,
    BadRequest
}
