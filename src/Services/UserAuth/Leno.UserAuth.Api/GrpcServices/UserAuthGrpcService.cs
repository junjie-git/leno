using Grpc.Core;
using Leno.SharedContracts.Grpc.User.V1;
using Leno.UserAuth.Application;

namespace Leno.UserAuth.Api.GrpcServices;

/// <summary>
/// 用户域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IUserInternalQueryService"/> 业务逻辑，与 InternalUsersController HTTP 路径双轨。
/// 鉴权策略：gRPC 仅依赖 <c>GrpcInternalKeyInterceptor</c> 拦截器统一校验 metadata <c>x-internal-key</c>，
/// 不依赖 JWT Bearer 鉴权管线（ASP.NET Core 默认未对 gRPC 启用 JWT Bearer）。
/// 故移除 <c>[Authorize]</c> 特性避免误导，拦截器在 <c>Program.cs</c> 中先于 gRPC 服务映射注册。
/// </summary>
public sealed class UserAuthGrpcService : UserInternalService.UserInternalServiceBase
{
    private readonly IUserInternalQueryService _queryService;
    private readonly ILogger<UserAuthGrpcService> _logger;

    public UserAuthGrpcService(
        IUserInternalQueryService queryService,
        ILogger<UserAuthGrpcService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<UserContacts> GetUserContacts(
        GetUserContactsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"UserId 格式无效：{request.UserId}"));
        }

        var dto = await _queryService.GetContactsAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"User {request.UserId} not found"));
        }

        return new UserContacts
        {
            UserId = dto.UserId.ToString(),
            Email = dto.Email ?? string.Empty,
            Phone = dto.PhoneNumber ?? string.Empty
            // 注：UserContactsDto 暂未提供 Nickname/EmailVerified/PhoneVerified/PreferredLanguage
            // proto 中这些 optional 字段保持默认值（空字符串/false），向后兼容
        };
    }
}
