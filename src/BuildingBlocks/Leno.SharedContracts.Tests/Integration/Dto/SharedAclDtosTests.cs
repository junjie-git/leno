// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Integration/Dto/SharedAclDtosTests.cs
using Leno.SharedContracts.Integration.Dto;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace Leno.SharedContracts.Tests.Integration.Dto;

/// <summary>
/// D2.1-D2.6 共享 ACL DTO 单元测试。
/// 验证 DTO 字段默认值、init 不可变性、JSON 序列化往返、工厂方法。
/// </summary>
public class SharedAclDtosTests
{
    // ========== D2.1 OrderStatusInfoDto / OrderItemStatusInfoDto ==========

    [Fact]
    public void OrderStatusInfoDto_Default_ShouldHaveEmptyItemsAndStatusText()
    {
        var dto = new OrderStatusInfoDto();

        dto.OrderId.Should().BeEmpty();
        dto.Status.Should().Be(0);
        dto.StatusText.Should().BeEmpty();
        dto.UserId.Should().BeEmpty();
        dto.SellerId.Should().BeEmpty();
        dto.CompletedAt.Should().Be(default);
        dto.CreatedAt.Should().Be(default);
        dto.Items.Should().NotBeNull().And.BeEmpty("Items 默认应初始化为空集合而非 null");
    }

    [Fact]
    public void OrderStatusInfoDto_WithItems_ShouldRoundTripThroughJson()
    {
        var orderId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var dto = new OrderStatusInfoDto
        {
            OrderId = orderId,
            Status = 5,
            StatusText = "Shipped",
            UserId = Guid.NewGuid(),
            SellerId = sellerId,
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new OrderItemStatusInfoDto
                {
                    OrderLineId = Guid.NewGuid(),
                    SkuId = skuId,
                    SpuId = Guid.NewGuid(),
                    SellerId = sellerId,
                    Quantity = 2,
                    AfterSalesStatus = 0
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<OrderStatusInfoDto>(json)!;

        deserialized.OrderId.Should().Be(orderId);
        deserialized.Status.Should().Be(5);
        deserialized.StatusText.Should().Be("Shipped");
        deserialized.Items.Should().HaveCount(1);
        deserialized.Items[0].SkuId.Should().Be(skuId);
        deserialized.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void OrderItemStatusInfoDto_Default_ShouldHaveEmptyGuids()
    {
        var dto = new OrderItemStatusInfoDto();

        dto.OrderLineId.Should().BeEmpty();
        dto.SkuId.Should().BeEmpty();
        dto.SpuId.Should().BeEmpty();
        dto.SellerId.Should().BeEmpty();
        dto.Quantity.Should().Be(0);
        dto.AfterSalesStatus.Should().Be(0);
    }

    // ========== D2.2 PaymentInfoDto ==========

    [Fact]
    public void PaymentInfoDto_Default_ShouldHaveEmptyStrings()
    {
        var dto = new PaymentInfoDto();

        dto.OrderId.Should().BeEmpty();
        dto.PaymentId.Should().BeEmpty();
        dto.AmountCents.Should().Be(0);
        dto.Status.Should().BeEmpty();
        dto.Channel.Should().BeEmpty();
        dto.PaidAt.Should().Be(default);
    }

    [Fact]
    public void PaymentInfoDto_WithValues_ShouldRoundTripThroughJson()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var paidAt = new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc);
        var dto = new PaymentInfoDto
        {
            OrderId = orderId,
            PaymentId = paymentId,
            AmountCents = 12345L,
            Status = "Paid",
            Channel = "WeChatPay",
            PaidAt = paidAt
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<PaymentInfoDto>(json)!;

        deserialized.OrderId.Should().Be(orderId);
        deserialized.PaymentId.Should().Be(paymentId);
        deserialized.AmountCents.Should().Be(12345L);
        deserialized.Status.Should().Be("Paid");
        deserialized.Channel.Should().Be("WeChatPay");
    }

    // ========== D2.3 ProductSnapshotDto ==========

    [Fact]
    public void ProductSnapshotDto_Default_ShouldHaveEmptyNameAndZeroPrice()
    {
        var dto = new ProductSnapshotDto();

        dto.SkuId.Should().BeEmpty();
        dto.SpuId.Should().BeEmpty();
        dto.Name.Should().BeEmpty();
        dto.SkuName.Should().BeEmpty();
        dto.Price.Should().Be(0m);
        dto.Stock.Should().Be(0);
        dto.ImageUrl.Should().BeNull();
        dto.IsOnSale.Should().BeFalse();
        dto.SellerId.Should().BeEmpty();
    }

    [Fact]
    public void ProductSnapshotDto_WithValues_ShouldRoundTripThroughJson()
    {
        var skuId = Guid.NewGuid();
        var spuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var dto = new ProductSnapshotDto
        {
            SkuId = skuId,
            SpuId = spuId,
            Name = "iPhone 17",
            SkuName = "256GB 黑色",
            Price = 7999.00m,
            Stock = 100,
            ImageUrl = "https://cdn.example.com/iphone17.jpg",
            IsOnSale = true,
            SellerId = sellerId
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<ProductSnapshotDto>(json)!;

        deserialized.SkuId.Should().Be(skuId);
        deserialized.SpuId.Should().Be(spuId);
        deserialized.Name.Should().Be("iPhone 17");
        deserialized.SkuName.Should().Be("256GB 黑色");
        deserialized.Price.Should().Be(7999.00m);
        deserialized.Stock.Should().Be(100);
        deserialized.IsOnSale.Should().BeTrue();
        deserialized.SellerId.Should().Be(sellerId);
    }

    // ========== D2.4 UserContactDto ==========

    [Fact]
    public void UserContactDto_Default_ShouldHaveNullEmailPhoneNickname()
    {
        var dto = new UserContactDto();

        dto.UserId.Should().BeEmpty();
        dto.Email.Should().BeNull("OAuth 注册用户可能无邮箱");
        dto.Phone.Should().BeNull("OAuth 注册用户可能无手机号");
        dto.Nickname.Should().BeNull();
    }

    [Fact]
    public void UserContactDto_Create_Factory_ShouldReturnCompatibleInstance()
    {
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var phone = "+8613800138000";

        var dto = UserContactDto.Create(userId, email, phone);

        dto.UserId.Should().Be(userId);
        dto.Email.Should().Be(email);
        dto.Phone.Should().Be(phone);
        dto.Nickname.Should().BeNull("工厂方法不填充 Nickname 以兼容旧 UserContactInfo");
    }

    [Fact]
    public void UserContactDto_WithNullableFields_ShouldRoundTripThroughJson()
    {
        var userId = Guid.NewGuid();
        var dto = new UserContactDto
        {
            UserId = userId,
            Email = null,
            Phone = "+8613800138000",
            Nickname = "Alice"
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<UserContactDto>(json)!;

        deserialized.UserId.Should().Be(userId);
        deserialized.Email.Should().BeNull();
        deserialized.Phone.Should().Be("+8613800138000");
        deserialized.Nickname.Should().Be("Alice");
    }

    // ========== D2.5 PointsFreezeResultDto / PointsConfirmResultDto / PointsReleaseResultDto ==========

    [Fact]
    public void PointsFreezeResultDto_Default_ShouldHaveCnyCurrencyAndSuccessTrue()
    {
        var dto = new PointsFreezeResultDto();

        dto.OrderId.Should().BeEmpty();
        dto.UserId.Should().BeEmpty();
        dto.PointsFrozen.Should().Be(0);
        dto.RemainingPoints.Should().Be(0);
        dto.OffsetAmount.Should().Be(0m);
        dto.Currency.Should().Be("CNY", "默认币种应为 CNY");
        dto.FrozenAt.Should().Be(default);
        dto.Success.Should().BeTrue("默认应标记为成功");
        dto.FailureCode.Should().BeEmpty();
    }

    [Fact]
    public void PointsFreezeResultDto_Failure_ShouldRoundTripThroughJson()
    {
        var orderId = Guid.NewGuid();
        var frozenAt = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var dto = new PointsFreezeResultDto
        {
            OrderId = orderId,
            UserId = Guid.NewGuid(),
            PointsFrozen = 500,
            RemainingPoints = 200,
            OffsetAmount = 5.00m,
            Currency = "CNY",
            FrozenAt = frozenAt,
            Success = false,
            FailureCode = "INSUFFICIENT_POINTS"
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<PointsFreezeResultDto>(json)!;

        deserialized.OrderId.Should().Be(orderId);
        deserialized.PointsFrozen.Should().Be(500);
        deserialized.RemainingPoints.Should().Be(200);
        deserialized.OffsetAmount.Should().Be(5.00m);
        deserialized.Success.Should().BeFalse();
        deserialized.FailureCode.Should().Be("INSUFFICIENT_POINTS");
    }

    [Fact]
    public void PointsConfirmResultDto_Default_ShouldHaveSuccessTrue()
    {
        var dto = new PointsConfirmResultDto();

        dto.OrderId.Should().BeEmpty();
        dto.PointsConfirmed.Should().Be(0);
        dto.RemainingPoints.Should().Be(0);
        dto.ConfirmedAt.Should().Be(default);
        dto.Success.Should().BeTrue();
        dto.FailureCode.Should().BeEmpty();
    }

    [Fact]
    public void PointsConfirmResultDto_WithValues_ShouldRoundTripThroughJson()
    {
        var orderId = Guid.NewGuid();
        var confirmedAt = new DateTime(2026, 7, 22, 11, 0, 0, DateTimeKind.Utc);
        var dto = new PointsConfirmResultDto
        {
            OrderId = orderId,
            PointsConfirmed = 500,
            RemainingPoints = 200,
            ConfirmedAt = confirmedAt,
            Success = true
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<PointsConfirmResultDto>(json)!;

        deserialized.OrderId.Should().Be(orderId);
        deserialized.PointsConfirmed.Should().Be(500);
        deserialized.RemainingPoints.Should().Be(200);
        deserialized.Success.Should().BeTrue();
    }

    [Fact]
    public void PointsReleaseResultDto_Default_ShouldHaveSuccessTrueAndIdempotentFalse()
    {
        var dto = new PointsReleaseResultDto();

        dto.OrderId.Should().BeEmpty();
        dto.PointsReleased.Should().Be(0);
        dto.RemainingPoints.Should().Be(0);
        dto.ReleasedAt.Should().Be(default);
        dto.Success.Should().BeTrue();
        dto.FailureCode.Should().BeEmpty();
        dto.IsIdempotentReturn.Should().BeFalse();
    }

    [Fact]
    public void PointsReleaseResultDto_IdempotentReturn_ShouldRoundTripThroughJson()
    {
        var orderId = Guid.NewGuid();
        var releasedAt = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var dto = new PointsReleaseResultDto
        {
            OrderId = orderId,
            PointsReleased = 0,
            RemainingPoints = 700,
            ReleasedAt = releasedAt,
            Success = true,
            IsIdempotentReturn = true
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<PointsReleaseResultDto>(json)!;

        deserialized.OrderId.Should().Be(orderId);
        deserialized.PointsReleased.Should().Be(0);
        deserialized.RemainingPoints.Should().Be(700);
        deserialized.IsIdempotentReturn.Should().BeTrue();
    }

    // ========== D2.6 DiscountCalculationResultDto / DiscountAllocationDto / CouponLockResultDto ==========

    [Fact]
    public void DiscountCalculationResultDto_Default_ShouldHaveEmptyAllocationsAndAppliedCouponIds()
    {
        var dto = new DiscountCalculationResultDto();

        dto.UserId.Should().BeEmpty();
        dto.TotalDiscountAmount.Should().Be(0m);
        dto.Currency.Should().Be("CNY");
        dto.Allocations.Should().NotBeNull().And.BeEmpty();
        dto.CalculatedAt.Should().Be(default);
        dto.AppliedCouponIds.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void DiscountCalculationResultDto_WithAllocationsAndCoupons_ShouldRoundTripThroughJson()
    {
        var userId = Guid.NewGuid();
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var coupon1 = Guid.NewGuid();
        var calculatedAt = new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc);
        var dto = new DiscountCalculationResultDto
        {
            UserId = userId,
            TotalDiscountAmount = 100.50m,
            Currency = "CNY",
            Allocations =
            [
                new DiscountAllocationDto { SkuId = sku1, Allocation = 60.00m },
                new DiscountAllocationDto { SkuId = sku2, Allocation = 40.50m }
            ],
            CalculatedAt = calculatedAt,
            AppliedCouponIds = [coupon1]
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<DiscountCalculationResultDto>(json)!;

        deserialized.UserId.Should().Be(userId);
        deserialized.TotalDiscountAmount.Should().Be(100.50m);
        deserialized.Allocations.Should().HaveCount(2);
        deserialized.Allocations[0].SkuId.Should().Be(sku1);
        deserialized.Allocations[0].Allocation.Should().Be(60.00m);
        deserialized.Allocations[1].SkuId.Should().Be(sku2);
        deserialized.Allocations[1].Allocation.Should().Be(40.50m);
        deserialized.AppliedCouponIds.Should().ContainSingle().Which.Should().Be(coupon1);
    }

    [Fact]
    public void DiscountAllocationDto_Default_ShouldHaveEmptySkuIdAndZeroAllocation()
    {
        var dto = new DiscountAllocationDto();

        dto.SkuId.Should().BeEmpty();
        dto.Allocation.Should().Be(0m);
    }

    [Fact]
    public void CouponLockResultDto_Default_ShouldHaveSuccessTrueAndEmptyFailureFields()
    {
        var dto = new CouponLockResultDto();

        dto.UserId.Should().BeEmpty();
        dto.CouponId.Should().BeEmpty();
        dto.OrderId.Should().BeEmpty();
        dto.UserCouponId.Should().BeEmpty();
        dto.LockedAt.Should().Be(default);
        dto.Success.Should().BeTrue();
        dto.FailureCode.Should().BeEmpty();
        dto.FailureMessage.Should().BeEmpty();
    }

    [Fact]
    public void CouponLockResultDto_Failure_ShouldRoundTripThroughJson()
    {
        var userId = Guid.NewGuid();
        var couponId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lockedAt = new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc);
        var dto = new CouponLockResultDto
        {
            UserId = userId,
            CouponId = couponId,
            OrderId = orderId,
            UserCouponId = Guid.Empty,
            LockedAt = lockedAt,
            Success = false,
            FailureCode = "COUPON_ALREADY_USED",
            FailureMessage = "优惠券已被其他订单使用"
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<CouponLockResultDto>(json)!;

        deserialized.UserId.Should().Be(userId);
        deserialized.CouponId.Should().Be(couponId);
        deserialized.OrderId.Should().Be(orderId);
        deserialized.Success.Should().BeFalse();
        deserialized.FailureCode.Should().Be("COUPON_ALREADY_USED");
        deserialized.FailureMessage.Should().Be("优惠券已被其他订单使用");
    }

    [Fact]
    public void CouponLockResultDto_Success_ShouldRoundTripThroughJson()
    {
        var userId = Guid.NewGuid();
        var couponId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userCouponId = Guid.NewGuid();
        var lockedAt = new DateTime(2026, 7, 22, 8, 30, 0, DateTimeKind.Utc);
        var dto = new CouponLockResultDto
        {
            UserId = userId,
            CouponId = couponId,
            OrderId = orderId,
            UserCouponId = userCouponId,
            LockedAt = lockedAt,
            Success = true
        };

        var json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<CouponLockResultDto>(json)!;

        deserialized.UserCouponId.Should().Be(userCouponId);
        deserialized.Success.Should().BeTrue();
        deserialized.FailureCode.Should().BeEmpty();
        deserialized.FailureMessage.Should().BeEmpty();
    }
}
