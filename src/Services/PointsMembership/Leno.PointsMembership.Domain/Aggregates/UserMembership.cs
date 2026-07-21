using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 用户会员权益聚合根，记录用户购买套餐获得的会员权益与有效期。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>UserMembershipId</c>。
/// </summary>
public sealed class UserMembership : AggregateRoot
{
    /// <summary>权益所属用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>购买的会员套餐标识。</summary>
    public Guid PackageId { get; private set; }

    /// <summary>套餐对应的会员等级编号。</summary>
    public int Level { get; private set; }

    /// <summary>权益生效时间（UTC）。</summary>
    public DateTime StartTime { get; private set; }

    /// <summary>权益到期时间（UTC）。</summary>
    public DateTime EndTime { get; private set; }

    /// <summary>权益状态。</summary>
    public UserMembershipStatus Status { get; private set; }

    /// <summary>触发权益的订单标识（支付成功后回填）。</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private UserMembership() { }

    private UserMembership(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验用户与套餐标识非空、等级 &gt; 0，初始状态为 Pending（待支付）。
    /// </summary>
    /// <param name="userMembershipId">权益标识，由应用层生成。</param>
    /// <param name="userId">所属用户标识。</param>
    /// <param name="packageId">套餐标识。</param>
    /// <param name="level">套餐对应会员等级编号，须 &gt; 0。</param>
    public static UserMembership Create(
        Guid userMembershipId,
        Guid userId,
        Guid packageId,
        int level)
    {
        if (userId == Guid.Empty)
        {
            throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
        }

        if (packageId == Guid.Empty)
        {
            throw new PointsDomainException("PackageId 不可为空", "PACKAGE_EMPTY");
        }

        if (level <= 0)
        {
            throw new PointsDomainException("等级编号须大于 0", "LEVEL_INVALID");
        }

        return new UserMembership(userMembershipId == Guid.Empty ? Guid.NewGuid() : userMembershipId)
        {
            UserId = userId,
            PackageId = packageId,
            Level = level,
            Status = UserMembershipStatus.Pending
        };
    }

    /// <summary>
    /// 激活权益（支付成功），设置生效起止时间、订单标识并置 Active，
    /// 发布 <see cref="MembershipActivatedEvent"/>。
    /// </summary>
    /// <param name="orderId">支付订单标识。</param>
    /// <param name="startTime">生效时间（UTC）。</param>
    /// <param name="durationDays">时长（天），须 &gt; 0。</param>
    public void Activate(Guid orderId, DateTime startTime, int durationDays)
    {
        if (orderId == Guid.Empty)
        {
            throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
        }

        // PM-H08 幂等：已激活且 OrderId 相同则直接返回，避免重复事件抛 MEMBERSHIP_ACTIVATE_INVALID
        // 触发 MassTransit 重试死循环（同订单重复支付成功事件场景）
        if (Status == UserMembershipStatus.Active && OrderId == orderId)
        {
            return;
        }

        if (durationDays <= 0)
        {
            throw new PointsDomainException("权益时长须大于 0", "MEMBERSHIP_DURATION_INVALID");
        }

        if (Status != UserMembershipStatus.Pending)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可激活，仅 Pending 可激活",
                "MEMBERSHIP_ACTIVATE_INVALID");
        }

        OrderId = orderId;
        StartTime = startTime;
        EndTime = startTime.AddDays(durationDays);
        Status = UserMembershipStatus.Active;
        AddDomainEvent(new MembershipActivatedEvent(UserId, PackageId, Level, EndTime));
    }

    /// <summary>
    /// 过期权益，仅 Active 态可过期。
    /// </summary>
    public void Expire()
    {
        if (Status != UserMembershipStatus.Active)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可过期，仅 Active 可过期",
                "MEMBERSHIP_EXPIRE_INVALID");
        }

        Status = UserMembershipStatus.Expired;
    }

    /// <summary>
    /// 取消权益（未支付订单取消），仅 Pending 态可取消。
    /// </summary>
    public void Cancel()
    {
        if (Status != UserMembershipStatus.Pending)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可取消，仅 Pending 可取消",
                "MEMBERSHIP_CANCEL_INVALID");
        }

        Status = UserMembershipStatus.Cancelled;
    }
}
