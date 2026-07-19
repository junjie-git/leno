using Grpc.Core;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedContracts.Grpc.Points.V1;
using Microsoft.AspNetCore.Authorization;

namespace Leno.PointsMembership.Api.GrpcServices;

/// <summary>
/// 积分会员域 gRPC 服务端（M4 双轨方案）。
/// 复用 <see cref="IPointsInternalAppService"/> 与 <see cref="IPointsAccountRepository"/> 业务逻辑，
/// 与 InternalPointsController HTTP 路径双轨。
/// 鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）。
/// </summary>
[Authorize]
public sealed class PointsGrpcService : PointsInternalService.PointsInternalServiceBase
{
    private readonly IPointsInternalAppService _internalAppService;
    private readonly IPointsAccountRepository _accountRepository;
    private readonly ILogger<PointsGrpcService> _logger;

    public PointsGrpcService(
        IPointsInternalAppService internalAppService,
        IPointsAccountRepository accountRepository,
        ILogger<PointsGrpcService> logger)
    {
        _internalAppService = internalAppService;
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public override async Task<TrialOffsetResponse> TrialOffset(
        TrialOffsetRequest request, ServerCallContext context)
    {
        var input = new TrialOffsetDto
        {
            UserId = new Guid(request.UserId),
            PointsToUse = request.PointsToUse
        };

        var result = await _internalAppService.TrialOffsetAsync(input, context.CancellationToken)
            .ConfigureAwait(false);

        return new TrialOffsetResponse
        {
            OffsetCents = (long)(result.OffsetAmount * 100),
            Success = true
        };
    }

    public override async Task<FreezeResponse> Freeze(FreezeRequest request, ServerCallContext context)
    {
        var input = new FreezePointsDto
        {
            UserId = new Guid(request.UserId),
            OrderId = new Guid(request.OrderId),
            PointsToUse = request.PointsToUse
        };

        await _internalAppService.FreezeAsync(input, context.CancellationToken)
            .ConfigureAwait(false);

        return new FreezeResponse { Success = true };
    }

    public override async Task<ReleaseResponse> Release(ReleaseRequest request, ServerCallContext context)
    {
        var input = new ReleasePointsDto { OrderId = new Guid(request.OrderId) };

        await _internalAppService.ReleaseAsync(input, context.CancellationToken)
            .ConfigureAwait(false);

        return new ReleaseResponse { Success = true };
    }

    public override async Task<ConfirmResponse> Confirm(ConfirmRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
        }

        var input = new ConfirmPointsDto(orderId);
        await _internalAppService.ConfirmAsync(input, context.CancellationToken)
            .ConfigureAwait(false);

        return new ConfirmResponse { Success = true };
    }

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
            Available = account.Balance,
            Frozen = account.FrozenBalance,
            Total = account.Balance + account.FrozenBalance
        };
    }
}
