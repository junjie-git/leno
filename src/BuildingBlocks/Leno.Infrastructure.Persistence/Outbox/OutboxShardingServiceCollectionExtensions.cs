using Leno.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// Outbox 分片发布器 DI 注册扩展（4.4 Outbox 分片发布器）。
/// <para>
/// 各 BC 在 <c>AddXxxInfrastructure</c> 中调用 <see cref="AddShardedOutboxPublisher{TDbContext}"/>
/// 注册 <see cref="ShardedOutboxPublisher{TDbContext}"/>，替代原有的 <see cref="OutboxPublisher{TDbContext}"/>。
/// </para>
/// <para>
/// 配置从 <c>Outbox:Sharding</c> 节绑定到 <see cref="OutboxShardingOptions"/>，
/// 也可通过环境变量 <c>OUTBOX__SHARDING__SHARD_ID</c> / <c>OUTBOX__SHARDING__SHARD_COUNT</c> 注入。
/// </para>
/// </summary>
public static class OutboxShardingServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Outbox 分片发布器与依赖。
    /// </summary>
    /// <typeparam name="TDbContext">承载发件箱表的 DbContext 类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置，用于绑定 <see cref="OutboxShardingOptions"/>。</param>
    /// <param name="useHashSharding">
    /// 是否使用 <see cref="HashShardingStrategy"/> 作为分片策略。默认 true。
    /// 设为 false 时由 DI 容器解析 <see cref="IShardingStrategy"/>（需自行注册）。
    /// </param>
    /// <returns>服务集合，便于链式调用。</returns>
    /// <example>
    /// <code>
    /// services.AddShardedOutboxPublisher&lt;OrderDbContext&gt;(configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddShardedOutboxPublisher<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useHashSharding = true)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 绑定配置
        services.Configure<OutboxShardingOptions>(options =>
        {
            configuration.GetSection(OutboxShardingOptions.SectionName).Bind(options);
        });

        // 注册分片策略（无状态单例）
        if (useHashSharding)
        {
            services.TryAddSingleton<IShardingStrategy>(HashShardingStrategy.Instance);
        }

        // 注册后台发布器
        services.AddHostedService<ShardedOutboxPublisher<TDbContext>>();

        return services;
    }

    /// <summary>
    /// 注册 Outbox 分片发布器，使用自定义配置回调。
    /// </summary>
    /// <typeparam name="TDbContext">承载发件箱表的 DbContext 类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <param name="configureOptions">配置回调，允许代码方式设置 <see cref="OutboxShardingOptions"/>。</param>
    /// <param name="useHashSharding">是否使用 <see cref="HashShardingStrategy"/>。默认 true。</param>
    /// <returns>服务集合，便于链式调用。</returns>
    /// <example>
    /// <code>
    /// services.AddShardedOutboxPublisher&lt;OrderDbContext&gt;(opts =&gt;
    /// {
    ///     opts.ShardCount = 8;
    ///     opts.ShardId = int.Parse(Environment.GetEnvironmentVariable("OUTBOX__SHARD_ID") ?? "0");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddShardedOutboxPublisher<TDbContext>(
        this IServiceCollection services,
        Action<OutboxShardingOptions> configureOptions,
        bool useHashSharding = true)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);

        if (useHashSharding)
        {
            services.TryAddSingleton<IShardingStrategy>(HashShardingStrategy.Instance);
        }

        services.AddHostedService<ShardedOutboxPublisher<TDbContext>>();

        return services;
    }
}
