using Leno.SellerShop.Api.GrpcServices;
using Leno.SellerShop.Application;
using Leno.SharedContracts.Grpc.Seller.V1;

namespace Leno.SellerShop.Api.Tests.GrpcServices;

/// <summary>
/// SellerGrpcService.MapToProto 单元测试。
/// 验证 SellerInfo/ShopInfo 的 ShopId 不再使用 Guid.GetHashCode() 不可逆映射，
/// 改为通过新增的 shop_id_str 字段承载 Guid.ToString()，避免跨 BC 哈希冲突。
/// </summary>
public sealed class SellerGrpcServiceMapToProtoTests
{
    [Fact]
    public void MapToProto_SellerInfo_Should_Populate_ShopIdStr_With_Guid_String()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var dto = new SellerInfoDto
        {
            SellerId = sellerId,
            Name = "测试卖家",
            Status = "Approved",
            ShopId = shopId
        };

        // Act — 通过反射调用私有静态方法 MapToProto(SellerInfoDto)
        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(SellerInfoDto) }, null);
        Assert.NotNull(method);
        var result = (SellerInfo)method!.Invoke(null, new object[] { dto })!;

        // Assert
        Assert.Equal(shopId.ToString(), result.ShopIdStr);
        Assert.Equal(sellerId.ToString(), result.SellerId);
        Assert.Equal("测试卖家", result.Name);
        Assert.Equal("Approved", result.Status);
        // int64 shop_id 不再使用 GetHashCode，应为 0（deprecated 字段）
        Assert.Equal(0L, result.ShopId);
    }

    [Fact]
    public void MapToProto_SellerInfo_Should_Not_Use_HashCode_For_ShopId()
    {
        // Arrange — 两个不同 Guid 不应映射到同一 long 值（GetHashCode 会冲突）
        var shopId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var shopId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var dto1 = new SellerInfoDto { SellerId = Guid.NewGuid(), Name = "卖家1", Status = "Active", ShopId = shopId1 };
        var dto2 = new SellerInfoDto { SellerId = Guid.NewGuid(), Name = "卖家2", Status = "Active", ShopId = shopId2 };

        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(SellerInfoDto) }, null);
        Assert.NotNull(method);
        var result1 = (SellerInfo)method!.Invoke(null, new object[] { dto1 })!;
        var result2 = (SellerInfo)method!.Invoke(null, new object[] { dto2 })!;

        // Assert — string 字段应不同且等于 Guid.ToString()
        Assert.NotEqual(result1.ShopIdStr, result2.ShopIdStr);
        Assert.Equal(shopId1.ToString(), result1.ShopIdStr);
        Assert.Equal(shopId2.ToString(), result2.ShopIdStr);
        // 两个 SellerInfo 的 int64 shop_id 均为 0（不再用 GetHashCode）
        Assert.Equal(0L, result1.ShopId);
        Assert.Equal(0L, result2.ShopId);
    }

    [Fact]
    public void MapToProto_ShopInfo_Should_Populate_ShopIdStr_With_Guid_String()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var dto = new ShopInfoDto
        {
            ShopId = shopId,
            Name = "测试店铺",
            Status = "Active",
            SellerId = sellerId
        };

        // Act
        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(ShopInfoDto) }, null);
        Assert.NotNull(method);
        var result = (ShopInfo)method!.Invoke(null, new object[] { dto })!;

        // Assert
        Assert.Equal(shopId.ToString(), result.ShopIdStr);
        Assert.Equal(sellerId.ToString(), result.SellerId);
        Assert.Equal("测试店铺", result.Name);
        Assert.Equal("Active", result.Status);
        // int64 shop_id 不再使用 GetHashCode
        Assert.Equal(0L, result.ShopId);
    }

    [Fact]
    public void MapToProto_ShopInfo_Should_Not_Use_HashCode_For_ShopId()
    {
        // Arrange — 两个不同 Guid 不应映射到同一 long 值
        var shopId1 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var shopId2 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var dto1 = new ShopInfoDto { ShopId = shopId1, Name = "店铺1", Status = "Active", SellerId = Guid.NewGuid() };
        var dto2 = new ShopInfoDto { ShopId = shopId2, Name = "店铺2", Status = "Active", SellerId = Guid.NewGuid() };

        var method = typeof(SellerGrpcService)
            .GetMethod("MapToProto",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(ShopInfoDto) }, null);
        Assert.NotNull(method);
        var result1 = (ShopInfo)method!.Invoke(null, new object[] { dto1 })!;
        var result2 = (ShopInfo)method!.Invoke(null, new object[] { dto2 })!;

        // Assert
        Assert.NotEqual(result1.ShopIdStr, result2.ShopIdStr);
        Assert.Equal(shopId1.ToString(), result1.ShopIdStr);
        Assert.Equal(shopId2.ToString(), result2.ShopIdStr);
        Assert.Equal(0L, result1.ShopId);
        Assert.Equal(0L, result2.ShopId);
    }
}
