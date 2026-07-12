using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class CategoryTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateEnabledCategory()
    {
        var category = Category.Create(Guid.NewGuid(), "Electronics");

        category.Name.Should().Be("Electronics");
        category.Level.Should().Be(1);
        category.Status.Should().Be(CategoryStatus.Enabled);
    }

    [Fact]
    public void Create_WithParent_ShouldSetHierarchy()
    {
        var parentId = Guid.NewGuid();

        var category = Category.Create(Guid.NewGuid(), "Phones", parentId, 1);

        category.ParentId.Should().Be(parentId);
        category.Level.Should().Be(2);
    }

    [Fact]
    public void Create_LevelTooDeep_ShouldThrowException()
    {
        var act = () => Category.Create(Guid.NewGuid(), "Deep", Guid.NewGuid(), 3);

        act.Should().Throw<ProductDomainException>().WithMessage("*层级*");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => Category.Create(Guid.NewGuid(), "");

        act.Should().Throw<ProductDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_NameTooLong_ShouldThrowException()
    {
        var act = () => Category.Create(Guid.NewGuid(), new string('A', 51));

        act.Should().Throw<ProductDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Update_ValidParameters_ShouldUpdateFields()
    {
        var category = CreateCategory();

        category.Update("Updated", 100);

        category.Name.Should().Be("Updated");
        category.SortOrder.Should().Be(100);
    }

    [Fact]
    public void Enable_Disabled_ShouldSetEnabled()
    {
        var category = CreateCategory();
        category.Disable();

        category.Enable();

        category.Status.Should().Be(CategoryStatus.Enabled);
    }

    [Fact]
    public void Disable_Enabled_ShouldSetDisabled()
    {
        var category = CreateCategory();

        category.Disable();

        category.Status.Should().Be(CategoryStatus.Disabled);
    }

    private static Category CreateCategory()
    {
        return Category.Create(Guid.NewGuid(), "Electronics");
    }
}