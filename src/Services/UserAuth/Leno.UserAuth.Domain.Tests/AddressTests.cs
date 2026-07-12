using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Tests;

public class AddressTests
{
    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateActiveAddress()
    {
        var address = Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Zhang San", "+8613800138000",
            "Zhejiang", "Hangzhou", "Xihu",
            "No. 123 Wenyi Road, Building 5, Room 301",
            "Home", true);

        address.RecipientName.Should().Be("Zhang San");
        address.Status.Should().Be(AddressStatus.Active);
        address.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Create_InvalidRecipientName_ShouldThrowException()
    {
        var act = () => Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "", "+8613800138000",
            "Zhejiang", "Hangzhou", "Xihu",
            "No. 123 Wenyi Road, Building 5, Room 301",
            null, false);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_InvalidPhone_ShouldThrowException()
    {
        var act = () => Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Zhang San", "invalid",
            "Zhejiang", "Hangzhou", "Xihu",
            "No. 123 Wenyi Road, Building 5, Room 301",
            null, false);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region UpdateInfo

    [Fact]
    public void UpdateInfo_ActiveAddress_ShouldUpdateFields()
    {
        var address = CreateAddress();
        address.UpdateInfo("Li Si", "+8613900139000",
            "Beijing", "Beijing", "Chaoyang",
            "朝阳区建国路88号SOHO现代城", "Work");

        address.RecipientName.Should().Be("Li Si");
        address.Province.Should().Be("Beijing");
        address.Tag.Should().Be("Work");
    }

    [Fact]
    public void UpdateInfo_DeletedAddress_ShouldThrowException()
    {
        var address = CreateAddress();
        address.SoftDelete();

        var act = () => address.UpdateInfo("Li Si", "+8613900139000",
            "Beijing", "Beijing", "Chaoyang",
            "朝阳区建国路88号SOHO现代城", "Work");

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region MarkAsDefault / UnmarkDefault

    [Fact]
    public void MarkAsDefault_ActiveAddress_ShouldSetDefault()
    {
        var address = CreateAddress(isDefault: false);
        address.MarkAsDefault();

        address.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void UnmarkDefault_ShouldClearDefault()
    {
        var address = CreateAddress(isDefault: true);
        address.UnmarkDefault();

        address.IsDefault.Should().BeFalse();
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_ActiveAddress_ShouldSetDeletedStatus()
    {
        var address = CreateAddress();
        address.SoftDelete();

        address.Status.Should().Be(AddressStatus.Deleted);
        address.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_AlreadyDeleted_ShouldThrowException()
    {
        var address = CreateAddress();
        address.SoftDelete();

        var act = () => address.SoftDelete();

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    private static Address CreateAddress(bool isDefault = false)
    {
        return Address.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Zhang San", "+8613800138000",
            "Zhejiang", "Hangzhou", "Xihu",
            "No. 123 Wenyi Road, Building 5, Room 301",
            "Home", isDefault);
    }
}