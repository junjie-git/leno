using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Events;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class FeatureFlagTests
{
    private static readonly Guid ValidFlagId = Guid.NewGuid();
    private const string ValidKey = "feature.new-checkout";
    private const string ValidName = "New Checkout Flow";
    private const string ValidDescription = "Enables the new checkout experience";
    private const string ValidRules = "{\"percentage\":50}";

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var flag = FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, ValidDescription, FeatureFlagStrategy.Percentage, ValidRules);

        flag.FlagId.Should().Be(ValidFlagId);
        flag.Id.Should().Be(ValidFlagId);
        flag.Key.Should().Be(ValidKey);
        flag.Name.Should().Be(ValidName);
        flag.Description.Should().Be(ValidDescription);
        flag.Strategy.Should().Be(FeatureFlagStrategy.Percentage);
        flag.Rules.Should().Be(ValidRules);
        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetDefaults()
    {
        var flag = FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, description: null, FeatureFlagStrategy.Global, rules: null);

        flag.Description.Should().BeNull();
        flag.Rules.Should().BeNull();
        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithAllStrategies_ShouldSucceed()
    {
        foreach (FeatureFlagStrategy strategy in Enum.GetValues<FeatureFlagStrategy>())
        {
            var flag = FeatureFlag.Create(Guid.NewGuid(), $"key.{strategy}", ValidName, null, strategy, null);
            flag.Strategy.Should().Be(strategy);
        }
    }

    [Fact]
    public void Create_ShouldTrimKeyAndName()
    {
        var flag = FeatureFlag.Create(ValidFlagId, "  feature.key  ", "  Feature Name  ", ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        flag.Key.Should().Be("feature.key");
        flag.Name.Should().Be("Feature Name");
    }

    [Fact]
    public void Create_WithWhitespaceDescriptionAndRules_ShouldNormalizeToNull()
    {
        var flag = FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, "   ", FeatureFlagStrategy.Global, "   ");

        flag.Description.Should().BeNull();
        flag.Rules.Should().BeNull();
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyFlagId_ShouldThrowFlagIdEmpty()
    {
        var act = () => FeatureFlag.Create(Guid.Empty, ValidKey, ValidName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullKey_ShouldThrowFlagKeyEmpty()
    {
        var act = () => FeatureFlag.Create(ValidFlagId, null!, ValidName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_KEY_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyKey_ShouldThrowFlagKeyEmpty()
    {
        var act = () => FeatureFlag.Create(ValidFlagId, "", ValidName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_KEY_EMPTY");
    }

    [Fact]
    public void Create_WithKeyTooLong_ShouldThrowFlagKeyLength()
    {
        var longKey = new string('k', 129);

        var act = () => FeatureFlag.Create(ValidFlagId, longKey, ValidName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_KEY_LENGTH");
    }

    [Fact]
    public void Create_WithKeyAtMaxLength_ShouldSucceed()
    {
        var key = new string('k', 128);

        var flag = FeatureFlag.Create(ValidFlagId, key, ValidName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        flag.Key.Should().Be(key);
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowFlagNameEmpty()
    {
        var act = () => FeatureFlag.Create(ValidFlagId, ValidKey, null!, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowFlagNameEmpty()
    {
        var act = () => FeatureFlag.Create(ValidFlagId, ValidKey, "", ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldThrowFlagNameLength()
    {
        var longName = new string('n', 129);

        var act = () => FeatureFlag.Create(ValidFlagId, ValidKey, longName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('n', 128);

        var flag = FeatureFlag.Create(ValidFlagId, ValidKey, name, ValidDescription, FeatureFlagStrategy.Global, ValidRules);

        flag.Name.Should().Be(name);
    }

    [Fact]
    public void Create_WithDescriptionTooLong_ShouldThrowFlagDescLength()
    {
        var longDesc = new string('d', 501);

        var act = () => FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, longDesc, FeatureFlagStrategy.Global, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_DESC_LENGTH");
    }

    [Fact]
    public void Create_WithDescriptionAtMaxLength_ShouldSucceed()
    {
        var desc = new string('d', 500);

        var flag = FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, desc, FeatureFlagStrategy.Global, ValidRules);

        flag.Description.Should().Be(desc);
    }

    [Fact]
    public void Create_WithInvalidStrategy_ShouldThrowFlagStrategyInvalid()
    {
        var invalidStrategy = (FeatureFlagStrategy)999;

        var act = () => FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, ValidDescription, invalidStrategy, ValidRules);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_STRATEGY_INVALID");
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateProperties()
    {
        var flag = CreateFlag();

        flag.Update("New Name", "New Description", FeatureFlagStrategy.UserWhitelist, "{\"users\":[\"a\",\"b\"]}");

        flag.Name.Should().Be("New Name");
        flag.Description.Should().Be("New Description");
        flag.Strategy.Should().Be(FeatureFlagStrategy.UserWhitelist);
        flag.Rules.Should().Be("{\"users\":[\"a\",\"b\"]}");
    }

    [Fact]
    public void Update_ShouldRaiseFeatureFlagChangedEvent()
    {
        var flag = CreateFlag();

        flag.Update("New Name", null, FeatureFlagStrategy.RoleBased, null);

        flag.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FeatureFlagChangedEvent>();
        var evt = (FeatureFlagChangedEvent)flag.DomainEvents.First();
        evt.FlagId.Should().Be(flag.Id);
        evt.FlagKey.Should().Be(ValidKey);
        evt.IsEnabled.Should().BeTrue();
        evt.Strategy.Should().Be((int)FeatureFlagStrategy.RoleBased);
    }

    [Fact]
    public void Update_WithNullName_ShouldThrowFlagNameEmpty()
    {
        var flag = CreateFlag();

        var act = () => flag.Update(null!, null, FeatureFlagStrategy.Global, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_NAME_EMPTY");
    }

    [Fact]
    public void Update_WithDescriptionTooLong_ShouldThrowFlagDescLength()
    {
        var flag = CreateFlag();
        var longDesc = new string('d', 501);

        var act = () => flag.Update(ValidName, longDesc, FeatureFlagStrategy.Global, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_DESC_LENGTH");
    }

    [Fact]
    public void Update_WithInvalidStrategy_ShouldThrowFlagStrategyInvalid()
    {
        var flag = CreateFlag();
        var invalidStrategy = (FeatureFlagStrategy)999;

        var act = () => flag.Update(ValidName, null, invalidStrategy, null);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("FLAG_STRATEGY_INVALID");
    }

    #endregion

    #region Enable

    [Fact]
    public void Enable_ShouldSetIsEnabledToTrue()
    {
        var flag = CreateFlag();
        flag.Disable();

        flag.Enable();

        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_ShouldRaiseFeatureFlagChangedEvent()
    {
        var flag = CreateFlag();
        flag.ClearDomainEvents();

        flag.Enable();

        flag.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FeatureFlagChangedEvent>();
        var evt = (FeatureFlagChangedEvent)flag.DomainEvents.First();
        evt.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region Disable

    [Fact]
    public void Disable_ShouldSetIsEnabledToFalse()
    {
        var flag = CreateFlag();

        flag.Disable();

        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_ShouldRaiseFeatureFlagChangedEvent()
    {
        var flag = CreateFlag();
        flag.ClearDomainEvents();

        flag.Disable();

        flag.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FeatureFlagChangedEvent>();
        var evt = (FeatureFlagChangedEvent)flag.DomainEvents.First();
        evt.IsEnabled.Should().BeFalse();
    }

    #endregion

    private static FeatureFlag CreateFlag()
    {
        return FeatureFlag.Create(ValidFlagId, ValidKey, ValidName, ValidDescription, FeatureFlagStrategy.Global, ValidRules);
    }
}