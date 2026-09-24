using Leno.UserCenter.Domain.Aggregates;
using Leno.UserCenter.Domain.Exceptions;
using Leno.UserCenter.Domain.ValueObjects;

namespace Leno.UserCenter.Domain.Tests.Aggregates;

/// <summary>
/// Address（收货地址聚合）单元测试。
/// 覆盖工厂校验（E.164 手机号、地区、标签长度）、状态守卫（软删除后不可改）、
/// 默认地址标记、软删除幂等等核心不变量。
/// </summary>
public class AddressTests
{
    private const string ValidPhone = "+8613800138000";

    private static Address CreateAddress(bool isDefault = false, string? tag = null) => Address.Create(
        Guid.NewGuid(), Guid.NewGuid(), "张三", ValidPhone,
        "广东省", "深圳市", "南山区", "科技园路 1 号 A 栋 1001 室",
        tag: tag, isDefault: isDefault);

    #region Create

    [Fact]
    public void Create_With_Valid_Input_Should_Initialize_Active_Address()
    {
        var userId = Guid.NewGuid();
        var tag = "家";

        var address = Address.Create(Guid.NewGuid(), userId, "张三", ValidPhone,
            " 广东省 ", " 深圳市 ", " 南山区 ", "  科技园路 1 号 A 栋 1001 室  ",
            tag: tag);

        address.UserId.Should().Be(userId);
        address.RecipientName.Should().Be("张三");
        address.RecipientPhone.Should().Be(ValidPhone);
        address.Province.Should().Be("广东省", "地区字段应 Trim");
        address.Status.Should().Be(AddressStatus.Active, "工厂方法创建的地址初始为 Active");
        address.IsDefault.Should().BeFalse();
        address.Tag.Should().Be(tag);
    }

    [Fact]
    public void Create_With_Empty_Tag_Should_Normalize_To_Null()
    {
        var address = CreateAddress(tag: "   ");

        address.Tag.Should().BeNull("空白标签应归一化为 null");
    }

    [Theory]
    [InlineData("13800138000", "缺少 + 前缀的本地格式")]
    [InlineData("+0123456789", "国家码 0 非法")]
    [InlineData("+86138001380000000000000", "超过 E.164 最长 15 位")]
    [InlineData("abc", "非数字")]
    public void Create_With_Invalid_Phone_Should_Throw_DomainException(string phone, string reason)
    {
        var create = () => Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "张三", phone,
            "广东省", "深圳市", "南山区", "科技园路 1 号");

        create.Should().Throw<UserCenterDomainException>()
            .WithMessage("*E.164*", reason);
    }

    [Fact]
    public void Create_With_Empty_Region_Should_Throw_DomainException()
    {
        var create = () => Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "张三", ValidPhone,
            "广东省", "  ", "南山区", "科技园路 1 号");

        create.Should().Throw<UserCenterDomainException>()
            .WithMessage("*市不可为空*");
    }

    [Fact]
    public void Create_With_OverLength_Tag_Should_Throw_DomainException()
    {
        var create = () => CreateAddress(tag: "超过八个字符的标签");

        create.Should().Throw<UserCenterDomainException>()
            .WithMessage("*8 字符*");
    }

    [Fact]
    public void Create_With_Empty_Ids_Should_Throw_DomainException()
    {
        var createEmptyId = () => Address.Create(Guid.Empty, Guid.NewGuid(), "张三", ValidPhone,
            "广东省", "深圳市", "南山区", "科技园路 1 号");
        var createEmptyUserId = () => Address.Create(Guid.NewGuid(), Guid.Empty, "张三", ValidPhone,
            "广东省", "深圳市", "南山区", "科技园路 1 号");

        createEmptyId.Should().Throw<UserCenterDomainException>().WithMessage("*地址标识不可为空*");
        createEmptyUserId.Should().Throw<UserCenterDomainException>().WithMessage("*用户标识不可为空*");
    }

    [Fact]
    public void Create_With_Too_Short_Detail_Should_Throw()
    {
        var create = () => Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "张三", ValidPhone,
            "广东省", "深圳市", "南山区", "短");

        create.Should().Throw<ArgumentException>("详细地址经 AddressDetail 校验（5-200 字符）");
    }

    #endregion

    #region UpdateInfo / 状态守卫

    [Fact]
    public void UpdateInfo_On_Active_Address_Should_Apply_Trimmed_Values()
    {
        var address = CreateAddress();

        address.UpdateInfo("李四", "+8613900139000", "北京市", "北京市", "朝阳区",
            "望京 SOHO T1 座 2500 室", tag: "公司");

        address.RecipientName.Should().Be("李四");
        address.RecipientPhone.Should().Be("+8613900139000");
        address.Province.Should().Be("北京市");
        address.Tag.Should().Be("公司");
    }

    [Fact]
    public void UpdateInfo_On_Deleted_Address_Should_Throw_DomainException()
    {
        var address = CreateAddress();
        address.SoftDelete();

        var update = () => address.UpdateInfo("李四", "+8613900139000",
            "北京市", "北京市", "朝阳区", "望京 SOHO T1 座 2500 室");

        update.Should().Throw<UserCenterDomainException>()
            .WithMessage("*仅 Active 状态的地址可修改*");
    }

    #endregion

    #region 默认地址标记

    [Fact]
    public void MarkAsDefault_On_Active_Address_Should_Set_Flag()
    {
        var address = CreateAddress();

        address.MarkAsDefault();

        address.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void MarkAsDefault_On_Deleted_Address_Should_Throw()
    {
        var address = CreateAddress(isDefault: true);
        address.SoftDelete();

        var mark = () => address.MarkAsDefault();

        mark.Should().Throw<UserCenterDomainException>();
    }

    [Fact]
    public void UnmarkDefault_Should_Clear_Flag_Even_On_Deleted_Address()
    {
        var address = CreateAddress(isDefault: true);
        address.SoftDelete();

        address.UnmarkDefault();

        address.IsDefault.Should().BeFalse();
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_Should_Set_Status_And_Clear_Default()
    {
        var address = CreateAddress(isDefault: true);

        address.SoftDelete();

        address.Status.Should().Be(AddressStatus.Deleted);
        address.IsDefault.Should().BeFalse("删除的地址不应再作为默认地址");
    }

    [Fact]
    public void SoftDelete_On_Already_Deleted_Should_Throw_DomainException()
    {
        var address = CreateAddress();
        address.SoftDelete();

        var delete = () => address.SoftDelete();

        delete.Should().Throw<UserCenterDomainException>()
            .WithMessage("*已删除*");
    }

    #endregion
}
