namespace Gradual.Models.DTOs;

/// <summary>
/// Generic service result wrapper for operation outcomes
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();

    public static ServiceResult<T> SuccessResult(T data, string message = "")
    {
        return new ServiceResult<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ServiceResult<T> FailureResult(string message, List<string>? errors = null)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }

    public static ServiceResult<T> FailureResult(string message, string error)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { error }
        };
    }
}
