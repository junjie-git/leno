using System.Globalization;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedContracts.Events;
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
    public void AdjustPrice_ValidSku_ShouldUpdatePriceAndRecordHistory()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.AdjustPrice(skuId, Money.Create(79.99m, "CNY"), "seller-1");

        sku.Price.Amount.Should().Be(79.99m);
        var history = spu.GetPriceHistory(skuId);
        history.Should().HaveCount(1);
        history[0].OldPrice.Should().Be(99.99m);
        history[0].NewPrice.Should().Be(79.99m);
        history[0].ChangedBy.Should().Be("seller-1");
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

    [Fact]
    public void GetPriceHistory_MultipleChanges_ShouldReturnAllForSku()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(100m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.AdjustPrice(skuId, Money.Create(90m, "CNY"), "seller-1");
        spu.AdjustPrice(skuId, Money.Create(80m, "CNY"), "seller-2");

        var history = spu.GetPriceHistory(skuId);
        history.Should().HaveCount(2);
        history[0].NewPrice.Should().Be(90m);
        history[1].NewPrice.Should().Be(80m);
    }

    [Fact]
    public void GetPriceHistory_NonExistentSku_ShouldReturnEmpty()
    {
        var spu = CreateDraftSpu();

        var history = spu.GetPriceHistory(Guid.NewGuid());
        history.Should().BeEmpty();
    }

    #endregion

    #region Stock Operations

    [Fact]
    public void UpdateStock_PositiveDelta_ShouldIncreaseStockAndRecordOperation()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.UpdateStock(skuId, 50, "operator-1");

        sku.StockQty.Should().Be(150);
        var history = spu.GetStockOperationHistory(skuId);
        history.Should().HaveCount(1);
        history[0].Delta.Should().Be(50);
        history[0].NewStock.Should().Be(150);
        history[0].Operator.Should().Be("operator-1");
    }

    [Fact]
    public void UpdateStock_NegativeDelta_ShouldDecreaseStockAndRecordOperation()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.UpdateStock(skuId, -30, "operator-1");

        sku.StockQty.Should().Be(70);
        var history = spu.GetStockOperationHistory(skuId);
        history[0].Delta.Should().Be(-30);
        history[0].NewStock.Should().Be(70);
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

        spu.DomainEvents.Should().Contain(e => e.GetType().Name == nameof(StockAdjustedEvent));
    }

    [Fact]
    public void GetStockOperationHistory_MultipleOperations_ShouldReturnAllForSku()
    {
        var spu = CreateDraftSpu();
        var skuId = Guid.NewGuid();
        var sku = SKU.Create(skuId, spu.Id, "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
        spu.AddSku(sku);

        spu.UpdateStock(skuId, 50, "operator-1");
        spu.UpdateStock(skuId, -20, "operator-2");

        var history = spu.GetStockOperationHistory(skuId);
        history.Should().HaveCount(2);
        history[0].Delta.Should().Be(50);
        history[1].Delta.Should().Be(-20);
    }

    #endregion

    #region Review Score

    [Fact]
    public void UpdateReviewScore_FirstReview_ShouldSetScoreAndCount()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.Score.Should().Be(0);
        spu.ReviewCount.Should().Be(0);

        // Act
        spu.UpdateReviewScore(5);

        // Assert
        spu.Score.Should().Be(5.0);
        spu.ReviewCount.Should().Be(1);
    }

    [Fact]
    public void UpdateReviewScore_MultipleReviews_ShouldCalculateWeightedAverage()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.UpdateReviewScore(5);
        spu.UpdateReviewScore(3);

        // Act
        spu.UpdateReviewScore(4);

        // Assert
        spu.Score.Should().Be(4.0); // (5+3+4)/3 = 4.0
        spu.ReviewCount.Should().Be(3);
    }

    [Fact]
    public void UpdateReviewScore_MixedRatings_ShouldCalculateCorrectly()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.UpdateReviewScore(1);
        spu.UpdateReviewScore(5);

        // Assert
        spu.Score.Should().Be(3.0); // (1+5)/2 = 3.0
        spu.ReviewCount.Should().Be(2);
    }

    [Fact]
    public void UpdateReviewScore_RatingZero_ShouldThrowException()
    {
        // Arrange
        var spu = CreateDraftSpu();

        // Act
        var act = () => spu.UpdateReviewScore(0);

        // Assert
        act.Should().Throw<ProductDomainException>()
            .Where(e => e.ErrorCode == "SPU_RATING_INVALID");
    }

    [Fact]
    public void UpdateReviewScore_RatingGreaterThanFive_ShouldThrowException()
    {
        // Arrange
        var spu = CreateDraftSpu();

        // Act
        var act = () => spu.UpdateReviewScore(6);

        // Assert
        act.Should().Throw<ProductDomainException>()
            .Where(e => e.ErrorCode == "SPU_RATING_INVALID");
    }

    [Fact]
    public void UpdateReviewScore_RatingNegative_ShouldThrowException()
    {
        // Arrange
        var spu = CreateDraftSpu();

        // Act
        var act = () => spu.UpdateReviewScore(-1);

        // Assert
        act.Should().Throw<ProductDomainException>()
            .Where(e => e.ErrorCode == "SPU_RATING_INVALID");
    }

    [Fact]
    public void RemoveReviewScore_WhenMultipleReviews_ShouldRecalculateCorrectly()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.UpdateReviewScore(5);
        spu.UpdateReviewScore(3);
        spu.UpdateReviewScore(4);
        // Score = 4.0, ReviewCount = 3

        // Act: remove rating 3 (the hidden review)
        spu.RemoveReviewScore(3);

        // Assert
        spu.Score.Should().Be(4.5); // (5+4)/2 = 4.5
        spu.ReviewCount.Should().Be(2);
    }

    [Fact]
    public void RemoveReviewScore_WhenSingleReview_ShouldResetToZero()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.UpdateReviewScore(4);
        spu.Score.Should().Be(4.0);
        spu.ReviewCount.Should().Be(1);

        // Act
        spu.RemoveReviewScore(4);

        // Assert
        spu.Score.Should().Be(0);
        spu.ReviewCount.Should().Be(0);
    }

    [Fact]
    public void RemoveReviewScore_WhenNoReviews_ShouldNotThrow()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.Score.Should().Be(0);
        spu.ReviewCount.Should().Be(0);

        // Act
        var act = () => spu.RemoveReviewScore(3);

        // Assert
        act.Should().NotThrow();
        spu.Score.Should().Be(0);
        spu.ReviewCount.Should().Be(0);
    }

    [Fact]
    public void RemoveReviewScore_RatingInvalid_ShouldThrowException()
    {
        // Arrange
        var spu = CreateDraftSpu();
        spu.UpdateReviewScore(4);

        // Act
        var act = () => spu.RemoveReviewScore(0);

        // Assert
        act.Should().Throw<ProductDomainException>()
            .Where(e => e.ErrorCode == "SPU_RATING_INVALID");
    }

    [Fact]
    public void UpdateAndRemoveReviewScore_FullCycle_ShouldBeCorrect()
    {
        // Arrange
        var spu = CreateDraftSpu();

        // Add reviews
        spu.UpdateReviewScore(5);
        spu.UpdateReviewScore(4);
        spu.UpdateReviewScore(3);
        spu.UpdateReviewScore(2);
        spu.UpdateReviewScore(1);

        spu.Score.Should().Be(3.0); // (5+4+3+2+1)/5 = 3.0
        spu.ReviewCount.Should().Be(5);

        // Hide the 1-star review
        spu.RemoveReviewScore(1);
        spu.Score.Should().Be(3.5); // (5+4+3+2)/4 = 3.5
        spu.ReviewCount.Should().Be(4);

        // Hide the 5-star review
        spu.RemoveReviewScore(5);
        spu.Score.Should().Be(3.0); // (4+3+2)/3 = 3.0
        spu.ReviewCount.Should().Be(3);

        // Add new review
        spu.UpdateReviewScore(5);
        spu.Score.Should().Be(3.5); // (4+3+2+5)/4 = 3.5
        spu.ReviewCount.Should().Be(4);
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