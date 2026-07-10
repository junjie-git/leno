namespace Leno.UserAuth.Domain.ValueObjects;

/// <summary>
/// 账户状态枚举，承载账户生命周期状态机。
/// 状态流转：Active ⇄ Locked（超时或管理员解锁回 Active）；Active/Locked → Disabled（终态，需管理员恢复）。
/// </summary>
public enum AccountStatus
{
    /// <summary>正常：注册即为此态，可正常登录。</summary>
    Active = 1,

    /// <summary>锁定：连续登录失败达阈值或管理员锁定，LockedUntil 超时后可解锁。</summary>
    Locked = 2,

    /// <summary>禁用：终态，需管理员恢复，禁止登录。</summary>
    Disabled = 3
}
