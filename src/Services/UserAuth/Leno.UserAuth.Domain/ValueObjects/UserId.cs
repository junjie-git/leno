namespace Leno.UserAuth.Domain.ValueObjects;

/// <summary>
/// 用户标识值对象，强类型包装 <see cref="Guid"/>，避免与其他聚合标识混淆。
/// 提供 <see cref="Guid"/> 隐式转换以便与共享内核仓储（按 Guid 查询）协作。
/// </summary>
public readonly record struct UserId
{
    /// <summary>标识值。</summary>
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("用户标识不可为空", nameof(value));
        }

        Value = value;
    }

    /// <summary>生成新的用户标识。</summary>
    public static UserId New() => new(Guid.NewGuid());

    /// <summary>空标识占位，使用 <see langword="default"/> 避免触发构造校验。</summary>
    public static UserId Empty => default;

    public static implicit operator Guid(UserId id) => id.Value;

    public static implicit operator UserId(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
