using System;
using System.Collections.Generic;
using System.Linq;
using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 通知渠道注册表实现，从 DI 注入的 <see cref="IEnumerable{INotificationChannel}"/> 构建。
/// 渠道实现自带 <see cref="INotificationChannel.Metadata"/>，注册表汇总后按 <see cref="NotificationChannelMetadata.Priority"/> 升序排序。
/// 新增渠道只需"实现 <see cref="INotificationChannel"/> + DI 注册"即可被注册表自动发现，零侵入核心调度逻辑。
/// </summary>
public sealed class NotificationChannelRegistry : INotificationChannelRegistry
{
    private readonly IReadOnlyDictionary<ChannelKey, NotificationChannelMetadata> _metadataByKey;
    private readonly IReadOnlyList<NotificationChannelMetadata> _orderedMetadata;
    private readonly ILogger<NotificationChannelRegistry> _logger;

    public NotificationChannelRegistry(
        IEnumerable<INotificationChannel> channels,
        ILogger<NotificationChannelRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        var channelList = channels as IList<INotificationChannel> ?? channels.ToList();

        // 按插入顺序去重：同一 ChannelKey 仅保留首个注册项，后续重复 Key 记录警告并忽略，
        // 保证 DI 注册顺序优先（避免低优先级配置覆盖高优先级实现）。
        var deduplicated = new List<NotificationChannelMetadata>(capacity: channelList.Count);
        var dict = new Dictionary<ChannelKey, NotificationChannelMetadata>(capacity: channelList.Count);
        foreach (var channel in channelList)
        {
            var metadata = channel.Metadata;
            if (dict.ContainsKey(metadata.Key))
            {
                _logger.LogWarning(
                    "渠道 Key 重复，已忽略后续注册 Key={ChannelKey} DisplayName={DisplayName}",
                    metadata.Key, metadata.DisplayName);
                continue;
            }

            dict[metadata.Key] = metadata;
            deduplicated.Add(metadata);
        }

        // 去重后再按 Priority 升序排序，供 GetAllChannels 返回有序视图。
        _orderedMetadata = deduplicated
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.Key.Value, StringComparer.Ordinal)
            .ToList();
        _metadataByKey = dict;

        _logger.LogInformation(
            "通知渠道注册表已初始化，共注册 {Count} 个渠道 Keys={Keys}",
            dict.Count, string.Join(",", dict.Keys.Select(k => (string)k)));
    }

    /// <inheritdoc />
    public IReadOnlyList<NotificationChannelMetadata> GetAllChannels()
    {
        return _orderedMetadata;
    }

    /// <inheritdoc />
    public NotificationChannelMetadata? GetChannel(ChannelKey key)
    {
        return _metadataByKey.TryGetValue(key, out var metadata) ? metadata : null;
    }

    /// <inheritdoc />
    public bool IsRegistered(ChannelKey key)
    {
        return _metadataByKey.ContainsKey(key);
    }

    /// <inheritdoc />
    public IEnumerable<NotificationChannelMetadata> GetChannelsByCapability(
        Func<NotificationChannelCapabilities, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _orderedMetadata.Where(m => predicate(m.Capabilities));
    }
}
