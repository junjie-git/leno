namespace Leno.SharedKernel.Abstractions;

/// <summary>
/// 审计字段契约，由 <see cref="BaseDbContext"/> 与审计拦截器统一填充。
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}

/// <summary>
/// 软删除契约，启用全局查询过滤器排除已删除记录。
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// 实体基类，提供标识与审计字段，所有领域实体继承此类。
/// </summary>
public abstract class Entity : IAuditable
{
    // T31：改为 init 替代 protected set，确保 Id 仅在构造阶段可赋值，
    // 构造完成后对外完全只读（包括子类）。EF Core 通过反射支持 init setter 物化。
    public Guid Id { get; init; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    protected Entity() { }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        if (Id == Guid.Empty || other.Id == Guid.Empty)
        {
            return false;
        }

        return Id == other.Id;
    }

    // T32：基于类型 + Id 计算哈希，避免未持久化实体（Id = Guid.Empty）跨类型哈希碰撞。
    // 原 Id.GetHashCode() 在 Guid.Empty 时所有临时实体哈希相同，影响 HashSet/Dictionary 性能。
    // HashCode.Combine(GetType(), Id) 使不同实体类型即使 Id 均为 Empty 也有不同哈希。
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
