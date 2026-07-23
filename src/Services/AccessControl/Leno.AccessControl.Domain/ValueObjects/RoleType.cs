namespace Leno.AccessControl.Domain.ValueObjects;

/// <summary>
/// 用户角色类型枚举，承载 RBAC 角色标识。
/// 内置角色：Buyer/Seller/Operator/Admin，对应 Token 角色声明。
/// 从 UserAuth BC 迁入 AccessControl BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public enum RoleType
{
    /// <summary>买家（注册即授予）。</summary>
    Buyer = 1,

    /// <summary>卖家。</summary>
    Seller = 2,

    /// <summary>运营。</summary>
    Operator = 3,

    /// <summary>系统管理员。</summary>
    Admin = 4
}
