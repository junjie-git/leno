namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 外部渠道配置抽象契约（支付、通知、存储等渠道统一配置驱动）。
/// 对应总览文档 4.8 节，领域层依赖此抽象，基础设施层按 Provider 提供适配器实现。
/// </summary>
public interface IExternalChannelOptions
{
    /// <summary>渠道提供商标识，如 WeChat、Alipay、Aliyun、SMTP。</summary>
    string Provider { get; }

    /// <summary>渠道参数字典，含 AppId、ApiKey 等敏感与非敏感参数。</summary>
    IReadOnlyDictionary<string, string?> Parameters { get; }
}
