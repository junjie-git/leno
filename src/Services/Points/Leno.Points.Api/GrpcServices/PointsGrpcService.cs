using Grpc.Core;
using Leno.Points.Application;
using Leno.Points.Domain.Repositories;
using Leno.SharedContracts.Grpc.Points.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.Points.Api.GrpcServices;

/// <summary>
/// 积分域 gRPC 服务端（域拆分后新 Points 域重建）。
/// 复用 <see cref="IPointsInternalAppService"/> 与 <see cref="IPointsAccountRepository"/> 业务逻辑，
/// 与 InternalPointsController HTTP 路径双轨（HTTP+gRPC 双轨重建，参见 Spec §2.2.1）。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class PointsGrpcService : PointsInternalService.PointsInternalServiceBase
{
    /// <summary>积分抵扣换算率：100 积分 = 1 元（与 PointsAccount 聚合保持一致）。</summary>
    private const int PointsPerYuan = 100;

    private readonly IPointsInternalAppService _internalAppService;
    private readonly IPointsAccountRepository _accountRepository;
    private readonly ILogger<PointsGrpcService> _logger;

    public PointsGrpcService(
        IPointsInternalAppService internalAppService,
        IPointsAccountRepository accountRepository,
        ILogger<PointsGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(internalAppService);
        ArgumentNullException.ThrowIfNull(accountRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _internalAppService = internalAppService;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    /// <summary>
    /// 试算积分可抵扣金额。
    /// proto 入参为 points_to_use（拟使用积分数量），新 Application 服务以 orderAmount（订单金额）入参，
    /// 这里按 100 积分 = 1 元换算将 points_to_use 转为 orderAmount，保证语义一致：
    /// 客户端询问"使用 X 积分能抵扣多少"，服务返回实际抵扣分（受余额约束）。
    /// </summary>
    public override async Task<TrialOffsetResponse> TrialOffset(
        TrialOffsetRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user id: {request.UserId}"));
        }

        // proto points_to_use → orderAmount（元）换算：100 积分 = 1 元
        var orderAmount = request.PointsToUse / (decimal)PointsPerYuan;

        var result = await _internalAppService.TrialOffsetAsync(userId, orderAmount, context.CancellationToken)
            .ConfigureAwait(false);

        return new TrialOffsetResponse
        {
            OffsetCents = (long)(result.OffsetAmount * 100),
            Success = true
        };
    }

    public override async Task<FreezeResponse> Freeze(FreezeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user id: {request.UserId}"));
        }
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order id: {request.OrderId}"));
        }

        await _internalAppService.FreezeAsync(userId, request.PointsToUse, orderId, context.CancellationToken)
            .ConfigureAwait(false);

        return new FreezeResponse { Success = true };
    }

    public override async Task<ReleaseResponse> Release(ReleaseRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order id: {request.OrderId}"));
        }

        await _internalAppService.ReleaseAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);

        return new ReleaseResponse { Success = true };
    }

    public override async Task<ConfirmResponse> Confirm(ConfirmRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
        }

        await _internalAppService.ConfirmAsync(orderId, context.CancellationToken)
            .ConfigureAwait(false);

        return new ConfirmResponse { Success = true };
    }

    /// <summary>
    /// 查询用户积分余额（可用/冻结/总额）。
    /// 直接走 IPointsAccountRepository 读路径，未经过应用服务（只读快照查询，无业务编排）。
    /// </summary>
    public override async Task<PointsBalance> GetPointsBalance(
        GetPointsBalanceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user id: {request.UserId}"));
        }

        var account = await _accountRepository.GetByUserIdAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Points account for {request.UserId} not found"));
        }

        return new PointsBalance
        {
            Available = account.Balance.Available,
            Frozen = account.Balance.Frozen,
            Total = account.Balance.Available + account.Balance.Frozen
        };
    }
}
