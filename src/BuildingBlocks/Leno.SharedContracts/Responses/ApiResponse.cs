namespace Leno.SharedContracts.Responses;

/// <summary>
/// 统一 API 响应结构（带数据载荷），对应总览第 8 章 RESTful 规范。
/// </summary>
public class ApiResponse<T>
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public string? TraceId { get; set; }
}

/// <summary>
/// 统一 API 响应结构（无数据载荷）。
/// 工厂方法集中在此非泛型类型上，避免在泛型类型上声明静态成员（CA1000）。
/// </summary>
public class ApiResponse
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? TraceId { get; set; }

    public static ApiResponse Success(string message = "success")
        => new() { Code = 200, Message = message };

    public static ApiResponse Fail(int code, string message)
        => new() { Code = code, Message = message };

    public static ApiResponse<T> Success<T>(T data, string message = "success")
        => new() { Code = 200, Message = message, Data = data };

    public static ApiResponse<T> Fail<T>(int code, string message, T? data = default)
        => new() { Code = code, Message = message, Data = data };
}
