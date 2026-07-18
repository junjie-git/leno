using System.Reflection;
using Leno.Infrastructure.Abstractions.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Infrastructure.Cqrs;

/// <summary>
/// Query Handler DI 注册扩展方法。
/// </summary>
public static class QueryHandlerExtensions
{
    /// <summary>
    /// 扫描指定程序集中的所有 IQueryHandler&lt;TQuery, TResult&gt; 实现并注册到 DI 容器。
    /// 默认生命周期为 Scoped（与 EF DbContext 一致，支持跨方法调用共享 DbContext）。
    /// </summary>
    /// <param name="services">IServiceCollection</param>
    /// <param name="assembly">包含 QueryHandler 实现的程序集</param>
    /// <param name="lifetime">DI 生命周期，默认 Scoped</param>
    /// <returns>IServiceCollection（链式调用）</returns>
    public static IServiceCollection AddQueryHandlers(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var handlerType = typeof(IQueryHandler<,>);

        var handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && !t.IsGenericType
                && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType))
            .ToList();

        foreach (var handlerImpl in handlers)
        {
            var implementedInterfaces = handlerImpl.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType)
                .ToList();

            foreach (var handlerInterface in implementedInterfaces)
            {
                services.Add(new ServiceDescriptor(handlerInterface, handlerImpl, lifetime));
            }
        }

        return services;
    }
}
