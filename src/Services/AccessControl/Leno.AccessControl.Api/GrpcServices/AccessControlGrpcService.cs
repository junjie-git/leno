using Grpc.Core;
using Leno.AccessControl.Application.Services;
using Leno.AccessControl.Domain.Services;
using Leno.SharedContracts.Grpc.AccessControl.V1;

namespace Leno.AccessControl.Api.GrpcServices;

/// <summary>
/// AccessControl BC gRPC 服务端实现（3.6 AuthN/AuthZ 拆分）。
/// 暴露 CheckPermission 与 GetUserRoles RPC，供 Identity BC（JwtTokenService 角色填充）、
/// API Gateway（权限校验）等跨 BC 调用方使用。
/// 鉴权策略：依赖 <c>GrpcInternalKeyInterceptor</c> 拦截器统一校验 metadata <c>x-internal-key</c>，
/// 不依赖 JWT Bearer 鉴权管线（与 UserAuthGrpcService 一致）。
/// </summary>
public sealed class AccessControlGrpcService : AccessControlService.AccessControlServiceBase
{
    private readonly IPermissionChecker _permissionChecker;
    private readonly IUserRoleAppService _userRoleAppService;
    private readonly ILogger<AccessControlGrpcService> _logger;

    public AccessControlGrpcService(
        IPermissionChecker permissionChecker,
        IUserRoleAppService userRoleAppService,
        ILogger<AccessControlGrpcService> logger)
    {
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _userRoleAppService = userRoleAppService ?? throw new ArgumentNullException(nameof(userRoleAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public override async Task<CheckPermissionResponse> CheckPermission(
        CheckPermissionRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"UserId 格式无效：{request.UserId}"));
        }

        if (string.IsNullOrWhiteSpace(request.Resource))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Resource 不可为空"));
        }

        var result = await _permissionChecker.CheckPermissionAsync(
            userId,
            request.Resource,
            string.IsNullOrWhiteSpace(request.Action) ? null : request.Action,
            context.CancellationToken).ConfigureAwait(false);

        var response = new CheckPermissionResponse
        {
            Allowed = result.Allowed,
            DenialReason = result.DenialReason ?? string.Empty
        };
        response.MatchedPolicies.AddRange(result.MatchedPolicies);

        return response;
    }

    /// <inheritdoc />
    public override async Task<GetUserRolesResponse> GetUserRoles(
        GetUserRolesRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"UserId 格式无效：{request.UserId}"));
        }

        var roles = await _userRoleAppService.GetUserRolesAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        var response = new GetUserRolesResponse();
        response.Roles.AddRange(roles);

        return response;
    }
}
