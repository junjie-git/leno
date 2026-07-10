using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Aggregates;

/// <summary>
/// 用户角色值对象，包装 <see cref="RoleType"/>，用于领域事件载荷与角色声明语义化传递。
/// 非聚合根，仅承载角色标识。
/// </summary>
public sealed record UserRole
{
    /// <summary>角色类型。</summary>
    public RoleType Value { get; }

    /// <summary>角色编码字符串，用于 Token 角色声明。</summary>
    public string Code => Value.ToString();

    public UserRole(RoleType value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "未定义的角色类型");
        }

        Value = value;
    }

    public static implicit operator RoleType(UserRole role) => role.Value;

    public static implicit operator UserRole(RoleType value) => new(value);

    public override string ToString() => Code;
}
