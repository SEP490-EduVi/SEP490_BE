namespace EduVi.Contracts.Common;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Result { get; set; } = default!;

    public static ApiResponse<T> Success(T? result, string message = "Thành công", int code = 200)
    {
        T? normalizedResult = result;
        if (typeof(T) == typeof(string) && result is null)
            normalizedResult = (T?)(object)string.Empty;

        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Result = normalizedResult
        };
    }

    public static ApiResponse<T> Fail(string message, int code)
    {
        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Result = default
        };
    }
}
