using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Domain.Tests;

/// <summary>
/// 验证 AfterSales / Review 聚合根 Images 集合的封装：
/// - 对外暴露为 IReadOnlyList&lt;string&gt;，编译期阻止 Add/Remove 等 mutate 操作；
/// - Create 工厂对入参 images 做防御性拷贝，外部 mutate 不影响聚合内部状态（合并审计 4.3）。
/// </summary>
public sealed class AggregateImagesEncapsulationTests
{
    [Fact]
    public void AfterSales_Images_Should_Be_ReadOnly_And_Not_Mutable_From_Outside()
    {
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken",
            new List<string> { "url1" }, 10m, "CNY");

        // IReadOnlyList<string> 无 Add/Remove 方法，编译期已阻止外部 mutate；
        // 运行期校验类型可分配给 IReadOnlyList<string>。
        afterSales.Images.Should().BeAssignableTo<IReadOnlyList<string>>();
        afterSales.Images.Should().HaveCount(1);
        afterSales.Images.Should().Contain("url1");
    }

    [Fact]
    public void AfterSales_Create_Should_Defensive_Copy_External_Images_List()
    {
        // 防御性拷贝验证：外部传入的 images 列表 mutate 不影响聚合（与 4.3 合并验证）
        var externalImages = new List<string> { "url1" };
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", externalImages, 10m, "CNY");

        // 外部 mutate 不应影响聚合内部状态
        externalImages.Add("url2");
        externalImages.Clear();

        afterSales.Images.Should().HaveCount(1);
        afterSales.Images.Should().Contain("url1");
    }

    [Fact]
    public void AfterSales_Create_Should_Accept_Null_Images()
    {
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken",
            images: null, 10m, "CNY");

        afterSales.Images.Should().BeEmpty();
        afterSales.Images.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public void Review_Images_Should_Be_ReadOnly_And_Not_Mutable_From_Outside()
    {
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", new List<string> { "url1" }, Guid.NewGuid());

        review.Images.Should().BeAssignableTo<IReadOnlyList<string>>();
        review.Images.Should().HaveCount(1);
        review.Images.Should().Contain("url1");
    }

    [Fact]
    public void Review_Create_Should_Defensive_Copy_External_Images_List()
    {
        var externalImages = new List<string> { "url1", "url2" };
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", externalImages, Guid.NewGuid());

        // 外部 mutate 不应影响聚合内部状态
        externalImages.Add("url3");
        externalImages.RemoveAt(0);

        review.Images.Should().HaveCount(2);
        review.Images.Should().Contain("url1");
        review.Images.Should().Contain("url2");
    }

    [Fact]
    public void Review_Create_Should_Accept_Null_Images()
    {
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", images: null, Guid.NewGuid());

        review.Images.Should().BeEmpty();
        review.Images.Should().BeAssignableTo<IReadOnlyList<string>>();
    }
}
