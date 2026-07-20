using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Leno.Testing.Builders;

/// <summary>
/// gRPC 防腐层测试辅助工具（P1-B.1）。
/// 为派生自 <see cref="GrpcAntiCorruptionClientBase"/> 的单元测试构造
/// 已注册 gRPC retry keyed 策略的 <see cref="IServiceProvider"/>，
/// 使用零延迟退避避免测试因指数退避产生秒级延迟，重试次数与生产一致（2 次）。
/// </summary>
public static class GrpcAntiCorruptionTestHelper
{
    /// <summary>
    /// 构造已注册 gRPC retry 策略的 <see cref="IServiceProvider"/>。
    /// 默认重试次数 2 次，零延迟退避，仅对临时性 gRPC 故障重试。
    /// </summary>
    /// <param name="retryCount">重试次数，默认 2（与生产配置一致）。</param>
    /// <returns>已注册 gRPC retry 策略的服务提供者。</returns>
    public static IServiceProvider BuildServiceProviderWithGrpcRetry(int retryCount = 2)
    {
        var services = new ServiceCollection();
        IAsyncPolicy retryPolicy = Policy
            .Handle<RpcException>(ex => AntiCorruptionPollyExtensions.IsTransientGrpcStatus(ex.StatusCode))
            .WaitAndRetryAsync(retryCount, _ => TimeSpan.Zero);
        services.AddKeyedSingleton<IAsyncPolicy>(AntiCorruptionPollyExtensions.GrpcRetryPolicyKey, retryPolicy);
        return services.BuildServiceProvider();
    }
}
