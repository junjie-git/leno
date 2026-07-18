namespace Leno.Infrastructure.Abstractions.Cqrs;

/// <summary>
/// CQRS 读侧 Query Handler 接口。不引入 MediatR，通过 DI 注册。
/// Query 不应产生副作用，仅查询读模型或既有仓储。
/// </summary>
/// <typeparam name="TQuery">查询参数类型，必须是 class（引用类型）</typeparam>
/// <typeparam name="TResult">查询结果类型</typeparam>
public interface IQueryHandler<in TQuery, TResult> where TQuery : class
{
    /// <summary>
    /// 异步执行查询。
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>查询结果</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
