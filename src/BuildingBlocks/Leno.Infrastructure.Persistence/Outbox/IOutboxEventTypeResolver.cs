using System.Collections.Concurrent;
using System.Reflection;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// 发件箱消息事件类型解析器抽象。
/// <para>
/// 默认实现 <see cref="DefaultOutboxEventTypeResolver"/> 优先按 <see cref="Type.FullName"/> 在已加载程序集中查找类型，
/// 兼容 BC 版本升级场景（如程序集版本号变更、命名空间迁移）——历史消息存储的
/// <c>AssemblyQualifiedName</c> 在新版本下可能无法通过 <see cref="Type.GetType(string)"/> 解析，
/// 此解析器会提取 FullName 并跨程序集匹配。
/// </para>
/// <para>
/// 业务上下文可提供自定义实现以处理更复杂的类型迁移（如命名空间重命名），
/// 通过 DI 注册 <c>IOutboxEventTypeResolver</c> 替换默认行为。
/// </para>
/// </summary>
public interface IOutboxEventTypeResolver
{
    /// <summary>
    /// 根据发件箱消息存储的类型字符串解析为 <see cref="Type"/>。
    /// </summary>
    /// <param name="typeName">
    /// 类型标识，可能是 <see cref="Type.AssemblyQualifiedName"/>、<see cref="Type.FullName"/> 或自定义格式。
    /// </param>
    /// <returns>解析到的 <see cref="Type"/>；无法解析时返回 null。</returns>
    Type? Resolve(string typeName);
}

/// <summary>
/// 默认事件类型解析器：按 FullName 跨已加载程序集解析，兼容版本升级。
/// <list type="number">
/// <item>优先调用 <see cref="Type.GetType(string)"/>（处理 AssemblyQualifiedName 与当前程序集内的 FullName）</item>
/// <item>失败时按 FullName 在所有已加载程序集中查找，缓存结果避免重复反射</item>
/// </list>
/// </summary>
public sealed class DefaultOutboxEventTypeResolver : IOutboxEventTypeResolver
{
    /// <summary>线程安全的单例，无状态可共享。</summary>
    public static readonly DefaultOutboxEventTypeResolver Instance = new();

    /// <summary>FullName → Type 缓存，避免每次发布都遍历程序集。</summary>
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public Type? Resolve(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        return TypeCache.GetOrAdd(typeName, ResolveUncached);
    }

    private static Type? ResolveUncached(string typeName)
    {
        // 1) 直接交给 Type.GetType，处理 AssemblyQualifiedName 与当前执行程序集内的 FullName
        var direct = Type.GetType(typeName);
        if (direct is not null)
        {
            return direct;
        }

        // 2) 提取 FullName：AssemblyQualifiedName 形如 "Ns.Type, Assembly, Version=..."
        //    取逗号前部分即为 FullName；若本身就是 FullName 则保持原值
        var fullName = ExtractFullName(typeName);
        if (string.IsNullOrEmpty(fullName) || fullName == typeName)
        {
            // typeName 本身就是 FullName，继续按 FullName 跨程序集查找
            return FindByFullName(fullName);
        }

        // 3) 按 FullName 在已加载程序集中查找（兼容程序集版本变更）
        return FindByFullName(fullName);
    }

    private static string ExtractFullName(string typeName)
    {
        var commaIndex = typeName.IndexOf(',');
        return commaIndex < 0 ? typeName : typeName.AsSpan(0, commaIndex).Trim().ToString();
    }

    private static Type? FindByFullName(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type;
            try
            {
                type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            }
            catch (ReflectionTypeLoadException)
            {
                // 部分程序集加载失败时跳过，继续在其它程序集中查找
                continue;
            }

            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }
}
