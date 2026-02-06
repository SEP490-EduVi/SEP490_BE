namespace EduVi.Contracts.Common;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Result { get; set; } = default!;

    public static ApiResponse<T> Success(T result, string message = "Success", int code = 200)
    {
        object value = result;

        if (typeof(T) == typeof(string) && result == null)
        {
            value = "";
        }

        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Result = (T)value
        };
    }

    public static ApiResponse<T> Fail(string message, int code)
    {
        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Result = default(T)
        };
    }
}
