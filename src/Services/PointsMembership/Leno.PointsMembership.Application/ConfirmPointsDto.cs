namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分确认扣减入参 DTO（跨 BC 内部查询，复用 <see cref="ReleasePointsDto"/> 单字段模式）。
/// 由订单域在支付成功后调用积分域 gRPC <c>Confirm</c> RPC 时构造。
/// </summary>
public sealed record ConfirmPointsDto(Guid OrderId);
