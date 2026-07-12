using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class SystemConfigTests
{
    private static readonly Guid ValidConfigId = Guid.NewGuid();
    private const string ValidKey = "app.timeout";
    private const string ValidValue = "30";
    private const string ValidGroup = "app";
    private const string ValidDescription = "Application timeout in seconds";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: true);

        config.ConfigId.Should().Be(ValidConfigId);
        config.Id.Should().Be(ValidConfigId);
        config.Key.Should().Be(ValidKey);
        config.Value.Should().Be(ValidValue);
        config.Group.Should().Be(ValidGroup);
        config.Description.Should().Be(ValidDescription);
        config.IsEncrypted.Should().BeTrue();
        config.Status.Should().Be(ConfigStatus.Enabled);
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetDefaults()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, description: null, isEncrypted: false);

        config.Description.Should().BeNull();
        config.IsEncrypted.Should().BeFalse();
        config.Status.Should().Be(ConfigStatus.Enabled);
    }

    [Fact]
    public void Create_WithWhitespaceDescription_ShouldNormalizeToNull()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, "   ", isEncrypted: false);

        config.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimKeyAndGroup()
    {
        var config = SystemConfig.Create(ValidConfigId, "  app.timeout  ", ValidValue, "  app  ", ValidDescription, isEncrypted: false);

        config.Key.Should().Be("app.timeout");
        config.Group.Should().Be("app");
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyConfigId_ShouldThrowConfigIdEmpty()
    {
        var act = () => SystemConfig.Create(Guid.Empty, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullKey_ShouldThrowConfigKeyEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, null!, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_KEY_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyKey_ShouldThrowConfigKeyEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, "", ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_KEY_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceKey_ShouldThrowConfigKeyEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, "   ", ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_KEY_EMPTY");
    }

    [Fact]
    public void Create_WithKeyTooLong_ShouldThrowConfigKeyLength()
    {
        var longKey = new string('k', 129);

        var act = () => SystemConfig.Create(ValidConfigId, longKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_KEY_LENGTH");
    }

    [Fact]
    public void Create_WithKeyAtMaxLength_ShouldSucceed()
    {
        var key = new string('k', 128);

        var config = SystemConfig.Create(ValidConfigId, key, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        config.Key.Should().Be(key);
    }

    [Fact]
    public void Create_WithNullValue_ShouldThrowConfigValueEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, null!, ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_VALUE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyValue_ShouldThrowConfigValueEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, "", ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_VALUE_EMPTY");
    }

    [Fact]
    public void Create_WithWhitespaceValue_ShouldThrowConfigValueEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, "   ", ValidGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_VALUE_EMPTY");
    }

    [Fact]
    public void Create_WithNullGroup_ShouldThrowConfigGroupEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, null!, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_GROUP_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyGroup_ShouldThrowConfigGroupEmpty()
    {
        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, "", ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_GROUP_EMPTY");
    }

    [Fact]
    public void Create_WithGroupTooLong_ShouldThrowConfigGroupLength()
    {
        var longGroup = new string('g', 65);

        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, longGroup, ValidDescription, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_GROUP_LENGTH");
    }

    [Fact]
    public void Create_WithGroupAtMaxLength_ShouldSucceed()
    {
        var group = new string('g', 64);

        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, group, ValidDescription, isEncrypted: false);

        config.Group.Should().Be(group);
    }

    [Fact]
    public void Create_WithDescriptionTooLong_ShouldThrowConfigDescLength()
    {
        var longDesc = new string('d', 501);

        var act = () => SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, longDesc, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_DESC_LENGTH");
    }

    [Fact]
    public void Create_WithDescriptionAtMaxLength_ShouldSucceed()
    {
        var desc = new string('d', 500);

        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, desc, isEncrypted: false);

        config.Description.Should().Be(desc);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateProperties()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, "30", ValidGroup, ValidDescription, isEncrypted: false);

        config.Update("60", "New description", isEncrypted: true);

        config.Value.Should().Be("60");
        config.Description.Should().Be("New description");
        config.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldRaiseConfigChangedEvent()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, "30", ValidGroup, ValidDescription, isEncrypted: false);

        config.Update("60", null, isEncrypted: false);

        config.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConfigChangedEvent>();
        var evt = (ConfigChangedEvent)config.DomainEvents.First();
        evt.ConfigId.Should().Be(config.Id);
        evt.ConfigKey.Should().Be(ValidKey);
        evt.ConfigValue.Should().Be("60");
    }

    [Fact]
    public void Update_WithNullValue_ShouldThrowConfigValueEmpty()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        var act = () => config.Update(null!, null, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_VALUE_EMPTY");
    }

    [Fact]
    public void Update_WithEmptyValue_ShouldThrowConfigValueEmpty()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        var act = () => config.Update("", null, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_VALUE_EMPTY");
    }

    [Fact]
    public void Update_WithDescriptionTooLong_ShouldThrowConfigDescLength()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);
        var longDesc = new string('d', 501);

        var act = () => config.Update(ValidValue, longDesc, isEncrypted: false);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("CONFIG_DESC_LENGTH");
    }

    [Fact]
    public void Update_WithWhitespaceDescription_ShouldNormalizeToNull()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        config.Update("60", "   ", isEncrypted: false);

        config.Description.Should().BeNull();
    }

    #endregion

    #region Enable

    [Fact]
    public void Enable_ShouldSetStatusToEnabled()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        config.Disable();
        config.Enable();

        config.Status.Should().Be(ConfigStatus.Enabled);
    }

    [Fact]
    public void Enable_ShouldRaiseConfigChangedEvent()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);
        config.ClearDomainEvents();

        config.Enable();

        config.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConfigChangedEvent>();
    }

    #endregion

    #region Disable

    [Fact]
    public void Disable_ShouldSetStatusToDisabled()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);

        config.Disable();

        config.Status.Should().Be(ConfigStatus.Disabled);
    }

    [Fact]
    public void Disable_ShouldRaiseConfigChangedEvent()
    {
        var config = SystemConfig.Create(ValidConfigId, ValidKey, ValidValue, ValidGroup, ValidDescription, isEncrypted: false);
        config.ClearDomainEvents();

        config.Disable();

        config.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConfigChangedEvent>();
    }

    #endregion
}