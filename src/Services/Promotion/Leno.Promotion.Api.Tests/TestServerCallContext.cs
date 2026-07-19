using Grpc.Core;

namespace Leno.Promotion.Api.Tests;

/// <summary>
/// gRPC 服务端单元测试用 <see cref="ServerCallContext"/> 最小实现。
/// 仅满足 PromotionGrpcService 直接调用所需成员，不涉及网络/调度。
/// 与 SellerShop / Cart / ReviewAfterSales 域 TestServerCallContext 风格保持一致。
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    protected override string MethodCore => "/test/Method";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "peer";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore { get; } = new();
    protected override Metadata ResponseTrailersCore { get; } = new();
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override AuthContext AuthContextCore
        => new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => null!;
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
