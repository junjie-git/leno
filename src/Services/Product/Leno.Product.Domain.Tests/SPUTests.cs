using System.Globalization;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Events;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class SPUTests
{
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Create_ValidParameters_ShouldCreateDraftSpu()
    {
        var spu = SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test Product",
            "https://img.example.com/1.jpg", CategoryId, images: []);

        spu.Title.Should().Be("Test Product");
        spu.Status.Should().Be(ProductStatus.Draft);
        spu.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrowException()
    {
        var act = () => SPU.Create(Guid.NewGuid(), ShopId, SellerId, "",
            "https://img.example.com/1.jpg", CategoryId);

        act.Should().Throw<ProductDomainException>().WithMessage("*标题*");
    }

    [Fact]
    public void Create_TitleTooLong_ShouldThrowException()
    {
        var act = () => SPU.Create(Guid.NewGuid(), ShopId, SellerId,
            new string('A', 101), "https://img.example.com/1.jpg", CategoryId);

        act.Should().Throw<ProductDomainException>().WithMessage("*标题*");
    }

    [Fact]
    public void Create_EmptyMainImage_ShouldThrowException()
    {
        var act = () => SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test",
            "", CategoryId);

        act.Should().Throw<ProductDomainException>().WithMessage("*主图*");
    }

    [Fact]
    public void Create_EmptyCategoryId_ShouldThrowException()
    {
        var act = () => SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test",
            "https://img.example.com/1.jpg", Guid.Empty);

        act.Should().Throw<ProductDomainException>().WithMessage("*分类*");
    }

    #region State Machine

    [Fact]
    public void SubmitForReview_WithSkus_ShouldTransitionToPendingReview()
    {
        var spu = CreateSpuWithSku();

        spu.SubmitForReview();

        spu.Status.Should().Be(ProductStatus.PendingReview);
    }

    [Fact]
    public void SubmitForReview_WithoutSkus_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.SubmitForReview();

        act.Should().Throw<ProductDomainException>().WithMessage("*SKU*");
    }

    [Fact]
    public void SubmitForReview_NotDraft_ShouldThrowException()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());

        var act = () => spu.SubmitForReview();

        act.Should().Throw<ProductDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Approve_PendingReview_ShouldTransitionToOnSale()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        var reviewerId = Guid.NewGuid();

        spu.Approve(reviewerId);

        spu.Status.Should().Be(ProductStatus.OnSale);
        spu.ReviewedBy.Should().Be(reviewerId);
        spu.DomainEvents.Should().Contain(e => e.GetType().Name.Contains("Reviewed"));
    }

    [Fact]
    public void Approve_NotPendingReview_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.Approve(Guid.NewGuid());

        act.Should().Throw<ProductDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Reject_PendingReview_ShouldTransitionToRejected()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();

        spu.Reject(Guid.NewGuid(), "Quality issues");

        spu.Status.Should().Be(ProductStatus.Rejected);
    }

    [Fact]
    public void Reject_EmptyReason_ShouldThrowException()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();

        var act = () => spu.Reject(Guid.NewGuid(), "");

        act.Should().Throw<ProductDomainException>().WithMessage("*原因*");
    }

    [Fact]
    public void TakeDown_OnSale_ShouldTransitionToTakenDown()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());

        spu.TakeDown("Out of stock");

        spu.Status.Should().Be(ProductStatus.TakenDown);
    }

    [Fact]
    public void TakeDown_NotOnSale_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.TakeDown("test");

        act.Should().Throw<ProductDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Republish_TakenDown_ShouldTransitionToPendingReview()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        spu.TakeDown("test");

        spu.Republish();

        spu.Status.Should().Be(ProductStatus.PendingReview);
    }

    [Fact]
    public void Republish_NotTakenDown_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.Republish();

        act.Should().Throw<ProductDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void FullStateMachine_ShouldFlowCorrectly()
    {
        var spu = CreateSpuWithSku();
        spu.Status.Should().Be(ProductStatus.Draft);

        spu.SubmitForReview();
        spu.Status.Should().Be(ProductStatus.PendingReview);

        spu.Reject(Guid.NewGuid(), "needs more info");
        spu.Status.Should().Be(ProductStatus.Rejected);

        // Rejected is terminal - cannot re-submit
        var act = () => spu.SubmitForReview();
        act.Should().Throw<ProductDomainException>();
    }

    #endregion

    #region Suspend/Resume By Shop

    [Fact]
    public void SuspendByShop_OnSale_ShouldTransitionToShopSuspended()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());

        spu.SuspendByShop();

        spu.Status.Should().Be(ProductStatus.ShopSuspended);
        spu.SuspendedByShop.Should().BeTrue();
    }

    [Fact]
    public void SuspendByShop_NotOnSale_ShouldBeSilentlyIgnored()
    {
        var spu = CreateDraftSpu();

        spu.SuspendByShop();

        spu.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public void ResumeByShop_SuspendedByShop_ShouldTransitionToOnSale()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        spu.SuspendByShop();

        spu.ResumeByShop();

        spu.Status.Should().Be(ProductStatus.OnSale);
    }

    [Fact]
    public void ResumeByShop_NotSuspendedByShop_ShouldBeSilentlyIgnored()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        spu.TakeDown("manual");

        spu.ResumeByShop();

        spu.Status.Should().Be(ProductStatus.TakenDown);
    }

    [Fact]
    public void TakeDownForShopClosure_OnSale_ShouldTransitionToTakenDown()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());

        spu.TakeDownForShopClosure("Shop closed");

        spu.Status.Should().Be(ProductStatus.TakenDown);
    }

    #endregion

    #region UpdateInfo

    [Fact]
    public void UpdateInfo_ValidInput_ShouldUpdateFields()
    {
        var spu = CreateDraftSpu();
        var newCategoryId = Guid.NewGuid();

        spu.UpdateInfo("Updated Title", "https://img.example.com/2.jpg", newCategoryId, "Subtitle", images: []);

        spu.Title.Should().Be("Updated Title");
        spu.Subtitle.Should().Be("Subtitle");
        spu.CategoryId.Should().Be(newCategoryId);
    }

    [Fact]
    public void UpdateInfo_TakenDown_ShouldThrowException()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        spu.TakeDown("test");

        var act = () => spu.UpdateInfo("Updated", "https://img.example.com/2.jpg", CategoryId, images: []);

        act.Should().Throw<ProductDomainException>().WithMessage("*下架*");
    }

    [Fact]
    public void UpdateInfo_Should_Publish_ProductUpdatedDomainEvent()
    {
        // Arrange
        var spu = CreateDraftSpu();
        var newImages = new[] { ProductImage.Create("https://cdn.example.com/new.png", 0, true) };

        // Act
        spu.UpdateInfo(
            title: "更新后的标题",
            mainImageUrl: "https://cdn.example.com/new.png",
            categoryId: CategoryId,
            subtitle: "新副标题",
            brandId: null,
            images: newImages);

        // Assert
        var domainEvent = spu.DomainEvents.OfType<ProductUpdatedDomainEvent>().SingleOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.ProductId.Should().Be(spu.Id);
        domainEvent.SellerId.Should().Be(spu.ShopId);
        domainEvent.Title.Should().Be("更新后的标题");
        domainEvent.MainImageUrl.Should().Be("https://cdn.example.com/new.png");
    }

    [Fact]
    public void UpdateSpecs_Should_Publish_ProductUpdatedDomainEvent()
    {
        // Arrange
        var spu = CreateDraftSpu();
        var newSpecs = new[] { "颜色", "尺码" };

        // Act
        spu.UpdateSpecs(newSpecs);

        // Assert
        var domainEvent = spu.DomainEvents.OfType<ProductUpdatedDomainEvent>().SingleOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.ProductId.Should().Be(spu.Id);
        domainEvent.SellerId.Should().Be(spu.ShopId);
    }

    #endregion

    #region SKU Management

    [Fact]
    public void AddSku_ValidSku_ShouldAddToList()
    {
        var spu = CreateDraftSpu();
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));

        spu.AddSku(sku);

        spu.SKUs.Should().HaveCount(1);
    }

    [Fact]
    public void AddSku_Should_Publish_ProductUpdatedDomainEvent()
    {
        // Arrange
        var spu = CreateDraftSpu();
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));

        // Act
        spu.AddSku(sku);

        // Assert
        var domainEvent = spu.DomainEvents.OfType<ProductUpdatedDomainEvent>().SingleOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.ProductId.Should().Be(spu.Id);
        domainEvent.SellerId.Should().Be(spu.ShopId);
    }

    [Fact]
    public void AddSku_TakenDown_ShouldThrowException()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        spu.TakeDown("test");

        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-002",
            Money.Create(50m, "CNY"), 10, SkuSpec.Create([SpecAttribute.Create("Size", "L")]));

        var act = () => spu.AddSku(sku);

        act.Should().Throw<ProductDomainException>().WithMessage("*下架*");
    }

    [Fact]
    public void AddSku_DuplicateCode_ShouldThrowException()
    {
        var spu = CreateDraftSpu();
        var sku1 = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku1);

        var sku2 = SKU.Create(Guid.NewGuid(), spu.Id, "sku-001",
            Money.Create(50m, "CNY"), 10, SkuSpec.Create([SpecAttribute.Create("Color", "Blue")]));

        var act = () => spu.AddSku(sku2);

        act.Should().Throw<ProductDomainException>().WithMessage("*SKU*编码*");
    }

    [Fact]
    public void AddSku_ExceedMaxLimit_ShouldThrowException()
    {
        var spu = CreateDraftSpu();
        for (int i = 0; i < 100; i++)
        {
            var sku = SKU.Create(Guid.NewGuid(), spu.Id, $"SKU-{i:D3}",
                Money.Create(10m, "CNY"), 1, SkuSpec.Create([SpecAttribute.Create("Index", i.ToString(CultureInfo.InvariantCulture))]));
            spu.AddSku(sku);
        }

        var extraSku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-100",
            Money.Create(10m, "CNY"), 1, SkuSpec.Create([SpecAttribute.Create("Index", "100")]));

        var act = () => spu.AddSku(extraSku);

        act.Should().Throw<ProductDomainException>().Which.Message.Should().ContainAny("100", "SKU");
    }

    [Fact]
    public void GetSku_ExistingId_ShouldReturnSku()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        var found = spu.GetSku(skuId);

        found.Should().Be(sku);
    }

    [Fact]
    public void GetSku_NonExistingId_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.GetSku(Guid.NewGuid());

        act.Should().Throw<ProductDomainException>().WithMessage("*SKU*");
    }

    #endregion

    #region Audit History

    [Fact]
    public void Approve_PendingReview_ShouldAppendAuditHistory()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        var reviewerId = Guid.NewGuid();

        spu.Approve(reviewerId, "Test Reviewer");

        var history = spu.GetAuditHistory();
        history.Should().HaveCount(1);
        history[0].Result.Should().Be("Approved");
        history[0].OperatorId.Should().Be(reviewerId.ToString());
        history[0].OperatorName.Should().Be("Test Reviewer");
    }

    [Fact]
    public void Reject_PendingReview_ShouldAppendAuditHistoryWithReason()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        var reviewerId = Guid.NewGuid();

        spu.Reject(reviewerId, "质量不合格", "Test Reviewer");

        var history = spu.GetAuditHistory();
        history.Should().HaveCount(1);
        history[0].Result.Should().Be("Rejected");
        history[0].Reason.Should().Be("质量不合格");
        history[0].OperatorName.Should().Be("Test Reviewer");
    }

    [Fact]
    public void GetAuditHistory_MultipleReviews_ShouldReturnAllRecords()
    {
        var spu1 = CreateSpuWithSku();
        spu1.SubmitForReview();
        var reviewer1 = Guid.NewGuid();
        spu1.Reject(reviewer1, "need fix", "Reviewer1");

        var spu2 = CreateSpuWithSku();
        spu2.SubmitForReview();
        var reviewer2 = Guid.NewGuid();
        spu2.Approve(reviewer2, "Reviewer2");

        spu1.GetAuditHistory().Should().HaveCount(1);
        spu1.GetAuditHistory()[0].Result.Should().Be("Rejected");
        spu2.GetAuditHistory().Should().HaveCount(1);
        spu2.GetAuditHistory()[0].Result.Should().Be("Approved");
    }

    [Fact]
    public void GetAuditHistory_NoReviews_ShouldReturnEmpty()
    {
        var spu = CreateDraftSpu();

        var history = spu.GetAuditHistory();
        history.Should().BeEmpty();
    }

    #endregion

    #region Price Change History

    [Fact]
    public void AdjustPrice_ValidSku_ShouldUpdatePriceAndReturnOldPrice()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        var oldPrice = spu.AdjustPrice(skuId, Money.Create(79.99m, "CNY"), "seller-1");

        sku.Price.Amount.Should().Be(79.99m);
        oldPrice.Should().Be(99.99m);
    }

    [Fact]
    public void AdjustPrice_NonExistentSku_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.AdjustPrice(Guid.NewGuid(), Money.Create(50m, "CNY"), "seller-1");

        act.Should().Throw<ProductDomainException>().WithMessage("*SKU*");
    }

    [Fact]
    public void AdjustPrice_EmptyChangedBy_ShouldThrowException()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        var act = () => spu.AdjustPrice(skuId, Money.Create(50m, "CNY"), "");

        act.Should().Throw<ProductDomainException>().WithMessage("*变更人*");
    }

    [Fact]
    public void AdjustPrice_TakenDown_ShouldThrowException()
    {
        var spu = CreateSpuWithSku();
        spu.SubmitForReview();
        spu.Approve(Guid.NewGuid());
        spu.TakeDown("test");
        var sku = spu.SKUs.First();

        var act = () => spu.AdjustPrice(sku.Id, Money.Create(50m, "CNY"), "seller-1");

        act.Should().Throw<ProductDomainException>().WithMessage("*下架*");
    }

    #endregion

    #region Stock Operations

    [Fact]
    public void UpdateStock_PositiveDelta_ShouldIncreaseStock()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.UpdateStock(skuId, 50, "operator-1");

        sku.StockQty.Should().Be(150);
    }

    [Fact]
    public void UpdateStock_NegativeDelta_ShouldDecreaseStock()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.UpdateStock(skuId, -30, "operator-1");

        sku.StockQty.Should().Be(70);
    }

    [Fact]
    public void UpdateStock_ResultNegative_ShouldThrowException()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 10, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        var act = () => spu.UpdateStock(skuId, -20, "operator-1");

        act.Should().Throw<ProductDomainException>().WithMessage("*库存*");
    }

    [Fact]
    public void UpdateStock_EmptyOperator_ShouldThrowException()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        var act = () => spu.UpdateStock(skuId, 10, "");

        act.Should().Throw<ProductDomainException>().WithMessage("*操作人*");
    }

    [Fact]
    public void UpdateStock_NonExistentSku_ShouldThrowException()
    {
        var spu = CreateDraftSpu();

        var act = () => spu.UpdateStock(Guid.NewGuid(), 10, "operator-1");

        act.Should().Throw<ProductDomainException>().WithMessage("*SKU*");
    }

    [Fact]
    public void UpdateStock_ShouldPublishStockAdjustedEvent()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.UpdateStock(skuId, 50, "operator-1");

        spu.DomainEvents.Should().Contain(e => e.GetType().Name == nameof(StockAdjustedDomainEvent));
    }

    #endregion

    private static SPU CreateDraftSpu()
    {
        return SPU.Create(Guid.NewGuid(), ShopId, SellerId, "Test Product",
            "https://img.example.com/1.jpg", CategoryId, images: []);
    }

    private static SPU CreateSpuWithSku()
    {
        var spu = CreateDraftSpu();
        var sku = SKU.Create(Guid.NewGuid(), spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);
        return spu;
    }
}