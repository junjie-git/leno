using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class BrandTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateEnabledBrand()
    {
        var brand = Brand.Create(Guid.NewGuid(), "Nike");

        brand.Name.Should().Be("Nike");
        brand.Status.Should().Be(BrandStatus.Enabled);
    }

    [Fact]
    public void Create_WithLogo_ShouldSetLogo()
    {
        var brand = Brand.Create(Guid.NewGuid(), "Nike", "https://logo.example.com/nike.png");

        brand.Logo.Should().Be("https://logo.example.com/nike.png");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => Brand.Create(Guid.NewGuid(), "");

        act.Should().Throw<ProductDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_NameTooLong_ShouldThrowException()
    {
        var act = () => Brand.Create(Guid.NewGuid(), new string('A', 51));

        act.Should().Throw<ProductDomainException>().WithMessage("*名称*");
    }

    [Fact]
    public void Create_LogoTooLong_ShouldThrowException()
    {
        var act = () => Brand.Create(Guid.NewGuid(), "Nike", new string('x', 513));

        act.Should().Throw<ProductDomainException>().WithMessage("*Logo*");
    }

    [Fact]
    public void Update_ValidParameters_ShouldUpdateFields()
    {
        var brand = CreateBrand();

        brand.Update("Adidas", "https://logo.example.com/adidas.png");

        brand.Name.Should().Be("Adidas");
        brand.Logo.Should().Be("https://logo.example.com/adidas.png");
    }

    [Fact]
    public void Enable_Disabled_ShouldSetEnabled()
    {
        var brand = CreateBrand();
        brand.Disable();

        brand.Enable();

        brand.Status.Should().Be(BrandStatus.Enabled);
    }

    [Fact]
    public void Disable_Enabled_ShouldSetDisabled()
    {
        var brand = CreateBrand();

        brand.Disable();

        brand.Status.Should().Be(BrandStatus.Disabled);
    }

    private static Brand CreateBrand()
    {
        return Brand.Create(Guid.NewGuid(), "Nike");
    }
}