using Leno.SharedKernel.Abstractions;

namespace Leno.Infrastructure.Tests.Abstractions;

/// <summary>
/// T31 + T32 单元测试：
/// T31 — Entity.Id 改为 init，验证构造后不可变、EF Core 反射赋值路径。
/// T32 — Entity.GetHashCode 改用 HashCode.Combine(GetType(), Id)，验证不同类型同 Guid.Empty 不碰撞。
/// </summary>
public class EntityInitAndHashCodeTests
{
    // ===== 测试用子类（模拟领域实体） =====

    private sealed class TestProductEntity : Entity
    {
        public TestProductEntity() { }
        public TestProductEntity(Guid id) : base(id) { }
    }

    private sealed class TestOrderEntity : Entity
    {
        public TestOrderEntity() { }
        public TestOrderEntity(Guid id) : base(id) { }
    }

    // ===== T31：Id init 不可变性测试 =====

    /// <summary>
    /// T31：通过构造函数传入 Id，构造后应可读。
    /// </summary>
    [Fact]
    public void Entity_Id_SetViaConstructor_ShouldBeReadable()
    {
        var id = Guid.NewGuid();
        var entity = new TestProductEntity(id);

        entity.Id.Should().Be(id);
    }

    /// <summary>
    /// T31：构造函数传入 Guid.Empty 时应自动生成新 Guid（保持原行为）。
    /// </summary>
    [Fact]
    public void Entity_Id_GuidEmpty_ShouldAutoGenerate()
    {
        var entity = new TestProductEntity(Guid.Empty);

        entity.Id.Should().NotBeEmpty();
    }

    /// <summary>
    /// T31：无参构造时 Id 应为 Guid.Empty（EF Core 物化前）。
    /// </summary>
    [Fact]
    public void Entity_Id_ParameterlessConstructor_ShouldBeEmpty()
    {
        var entity = new TestProductEntity();

        entity.Id.Should().BeEmpty();
    }

    /// <summary>
    /// T31：init 属性应可通过反射赋值，模拟 EF Core 物化路径。
    /// </summary>
    [Fact]
    public void Entity_Id_Init_ShouldBeSettableViaReflection_EfCorePath()
    {
        // Arrange
        var entity = new TestProductEntity();
        entity.Id.Should().BeEmpty(); // 物化前为 Empty

        // Act — EF Core 通过反射设置 init 属性
        var idProp = typeof(Entity).GetProperty(nameof(Entity.Id));
        var generatedId = Guid.NewGuid();
        idProp!.SetValue(entity, generatedId);

        // Assert
        entity.Id.Should().Be(generatedId);
    }

    /// <summary>
    /// T31：Id 属性的 CanWrite 应为 true（init setter 编译为可写访问器）。
    /// </summary>
    [Fact]
    public void Entity_Id_CanWrite_ShouldBeTrue()
    {
        var idProp = typeof(Entity).GetProperty(nameof(Entity.Id));
        idProp!.CanWrite.Should().BeTrue("Id 应有 init setter 可写");
    }

    // ===== T32：GetHashCode HashCode.Combine 测试 =====

    /// <summary>
    /// T32：两个相同类型、相同 Id 的实体应产生相同哈希。
    /// </summary>
    [Fact]
    public void GetHashCode_SameTypeSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var e1 = new TestProductEntity(id);
        var e2 = new TestProductEntity(id);

        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    /// <summary>
    /// T32：不同类型、相同 Id 的实体应产生不同哈希（核心修复点）。
    /// 原 Id.GetHashCode() 会碰撞；HashCode.Combine(GetType(), Id) 不碰撞。
    /// </summary>
    [Fact]
    public void GetHashCode_DifferentTypesSameId_ShouldDiffer()
    {
        var id = Guid.NewGuid();
        var product = new TestProductEntity(id);
        var order = new TestOrderEntity(id);

        product.GetHashCode().Should().NotBe(order.GetHashCode());
    }

    /// <summary>
    /// T32：不同类型、均为 Guid.Empty 的未持久化实体应产生不同哈希（核心修复点）。
    /// 原 Id.GetHashCode() 在 Guid.Empty 时所有临时实体哈希相同，影响 HashSet 性能。
    /// </summary>
    [Fact]
    public void GetHashCode_DifferentTypesBothEmpty_ShouldDiffer()
    {
        var product = new TestProductEntity(); // Id = Empty
        var order = new TestOrderEntity();     // Id = Empty

        product.GetHashCode().Should().NotBe(order.GetHashCode());
    }

    /// <summary>
    /// T32：相同类型、不同 Id 的实体应产生不同哈希（极大概率，非绝对保证）。
    /// </summary>
    [Fact]
    public void GetHashCode_SameTypeDifferentId_ShouldDiffer()
    {
        var e1 = new TestProductEntity(Guid.NewGuid());
        var e2 = new TestProductEntity(Guid.NewGuid());

        e1.GetHashCode().Should().NotBe(e2.GetHashCode());
    }

    /// <summary>
    /// T32：HashSet 中放入多个不同类型的未持久化实体（Id = Empty），应能区分存储，
    /// 不会因哈希碰撞退化为线性链。验证修复后 HashSet.Count 正确。
    /// </summary>
    [Fact]
    public void GetHashCode_HashSetOfDifferentTypesWithEmptyId_ShouldStoreAll()
    {
        // Arrange — 创建多个不同类型的未持久化实体
        var entities = new HashSet<Entity>
        {
            new TestProductEntity(), // Id = Empty
            new TestOrderEntity(),   // Id = Empty
        };

        // Assert — 两个不同类型的实体都在 HashSet 中（即使 Id 均为 Empty）
        // 注意：Equals 在 Id=Empty 时返回 false，所以两者不等价
        entities.Should().HaveCount(2);
    }

    /// <summary>
    /// T32：相同类型、多个未持久化实体（Id = Empty）在 HashSet 中，
    /// 因 Equals 在 Id=Empty 时返回 false，应都能存储（不互相等价）。
    /// 哈希相同但 Equals 不同 → 链桶存储，验证不丢失。
    /// </summary>
    [Fact]
    public void GetHashCode_HashSetSameTypeMultipleEmptyId_AllStoredDueToEqualsFalse()
    {
        var entities = new HashSet<Entity>
        {
            new TestProductEntity(),
            new TestProductEntity(),
            new TestProductEntity(),
        };

        // Equals 在 Id=Empty 时返回 false，所以三个实体互不等价，都在 HashSet 中
        entities.Should().HaveCount(3);
    }

    /// <summary>
    /// T32：Equals 行为不变 — 相同类型、相同非空 Id 的两个实体应相等。
    /// </summary>
    [Fact]
    public void Equals_SameTypeSameNonEmptyId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var e1 = new TestProductEntity(id);
        var e2 = new TestProductEntity(id);

        e1.Equals(e2).Should().BeTrue();
        (e1 == e2).Should().BeTrue();
    }

    /// <summary>
    /// T32：Equals 行为不变 — 不同类型的两个实体应不相等（即使 Id 相同）。
    /// </summary>
    [Fact]
    public void Equals_DifferentTypesSameId_ShouldNotBeEqual()
    {
        var id = Guid.NewGuid();
        var product = new TestProductEntity(id);
        var order = new TestOrderEntity(id);

        product.Equals(order).Should().BeFalse();
        (product == order).Should().BeFalse();
    }

    /// <summary>
    /// T32：Equals 行为不变 — Id 为 Empty 时不相等（未持久化实体不判等）。
    /// </summary>
    [Fact]
    public void Equals_BothEmptyId_ShouldNotBeEqual()
    {
        var e1 = new TestProductEntity();
        var e2 = new TestProductEntity();

        e1.Equals(e2).Should().BeFalse();
    }
}
