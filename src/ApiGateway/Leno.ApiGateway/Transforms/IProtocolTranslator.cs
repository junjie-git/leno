using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// 协议转换抽象接口。当前不实现具体转换逻辑，待 gRPC 迁移后填充实现。
/// <para>
/// 在 YARP 管道中预留注入点，当后端服务提供 gRPC 端点后，
/// 注册对应 <see cref="IProtocolTranslator"/> 实现即可启用 HTTP↔gRPC 转换。
/// </para>
/// </summary>
public interface IProtocolTranslator
{
    /// <summary>源协议（如 "HTTP"）。</summary>
    string SourceProtocol { get; }

    /// <summary>目标协议（如 "gRPC"）。</summary>
    string TargetProtocol { get; }

    /// <summary>将源协议请求转换为目标协议请求。</summary>
    Task<HttpRequestMessage> TranslateRequestAsync(HttpContext context);

    /// <summary>将后端响应转换回源协议格式写入客户端。</summary>
    Task TranslateResponseAsync(HttpContext context, HttpResponseMessage response);
}
