using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class DataDictionaryTests
{
    private static readonly Guid ValidDictionaryId = Guid.NewGuid();
    private const string ValidCode = "GENDER";
    private const string ValidName = "Gender";
    private const string ValidDescription = "Gender options dictionary";

    private static readonly Guid ValidItemId = Guid.NewGuid();
    private const string ValidItemCode = "MALE";
    private const string ValidItemLabel = "Male";
    private const string ValidItemValue = "1";

    #region DataDictionary Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var dict = DataDictionary.Create(ValidDictionaryId, ValidCode, ValidName, ValidDescription);

        dict.DictionaryId.Should().Be(ValidDictionaryId);
        dict.Id.Should().Be(ValidDictionaryId);
        dict.Code.Should().Be(ValidCode);
        dict.Name.Should().Be(ValidName);
        dict.Description.Should().Be(ValidDescription);
        dict.Status.Should().Be(DictionaryStatus.Enabled);
        dict.Items.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithMinimalParameters_ShouldSetDefaults()
    {
        var dict = DataDictionary.Create(ValidDictionaryId, ValidCode, ValidName, description: null);

        dict.Description.Should().BeNull();
        dict.Status.Should().Be(DictionaryStatus.Enabled);
    }

    [Fact]
    public void Create_ShouldTrimCodeAndName()
    {
        var dict = DataDictionary.Create(ValidDictionaryId, "  GENDER  ", "  Gender  ", ValidDescription);

        dict.Code.Should().Be("GENDER");
        dict.Name.Should().Be("Gender");
    }

    [Fact]
    public void Create_WithWhitespaceDescription_ShouldNormalizeToNull()
    {
        var dict = DataDictionary.Create(ValidDictionaryId, ValidCode, ValidName, "   ");

        dict.Description.Should().BeNull();
    }

    #endregion

    #region DataDictionary Create - Validation

    [Fact]
    public void Create_WithEmptyDictionaryId_ShouldThrowDictIdEmpty()
    {
        var act = () => DataDictionary.Create(Guid.Empty, ValidCode, ValidName, ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullCode_ShouldThrowDictCodeEmpty()
    {
        var act = () => DataDictionary.Create(ValidDictionaryId, null!, ValidName, ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_CODE_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyCode_ShouldThrowDictCodeEmpty()
    {
        var act = () => DataDictionary.Create(ValidDictionaryId, "", ValidName, ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_CODE_EMPTY");
    }

    [Fact]
    public void Create_WithCodeTooLong_ShouldThrowDictCodeLength()
    {
        var longCode = new string('c', 65);

        var act = () => DataDictionary.Create(ValidDictionaryId, longCode, ValidName, ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_CODE_LENGTH");
    }

    [Fact]
    public void Create_WithCodeAtMaxLength_ShouldSucceed()
    {
        var code = new string('c', 64);

        var dict = DataDictionary.Create(ValidDictionaryId, code, ValidName, ValidDescription);

        dict.Code.Should().Be(code);
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowDictNameEmpty()
    {
        var act = () => DataDictionary.Create(ValidDictionaryId, ValidCode, null!, ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowDictNameEmpty()
    {
        var act = () => DataDictionary.Create(ValidDictionaryId, ValidCode, "", ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithNameTooLong_ShouldThrowDictNameLength()
    {
        var longName = new string('n', 129);

        var act = () => DataDictionary.Create(ValidDictionaryId, ValidCode, longName, ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('n', 128);

        var dict = DataDictionary.Create(ValidDictionaryId, ValidCode, name, ValidDescription);

        dict.Name.Should().Be(name);
    }

    [Fact]
    public void Create_WithDescriptionTooLong_ShouldThrowDictDescLength()
    {
        var longDesc = new string('d', 501);

        var act = () => DataDictionary.Create(ValidDictionaryId, ValidCode, ValidName, longDesc);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_DESC_LENGTH");
    }

    [Fact]
    public void Create_WithDescriptionAtMaxLength_ShouldSucceed()
    {
        var desc = new string('d', 500);

        var dict = DataDictionary.Create(ValidDictionaryId, ValidCode, ValidName, desc);

        dict.Description.Should().Be(desc);
    }

    #endregion

    #region DataDictionary Update

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateNameAndDescription()
    {
        var dict = CreateDictionary();

        dict.Update("New Gender", "Updated description");

        dict.Name.Should().Be("New Gender");
        dict.Description.Should().Be("Updated description");
    }

    [Fact]
    public void Update_WithNullDescription_ShouldSetNull()
    {
        var dict = CreateDictionary();

        dict.Update(ValidName, null);

        dict.Description.Should().BeNull();
    }

    [Fact]
    public void Update_WithEmptyName_ShouldThrowDictNameEmpty()
    {
        var dict = CreateDictionary();

        var act = () => dict.Update("", ValidDescription);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_NAME_EMPTY");
    }

    [Fact]
    public void Update_WithDescriptionTooLong_ShouldThrowDictDescLength()
    {
        var dict = CreateDictionary();
        var longDesc = new string('d', 501);

        var act = () => dict.Update(ValidName, longDesc);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_DESC_LENGTH");
    }

    #endregion

    #region DataDictionary Enable / Disable

    [Fact]
    public void Enable_ShouldSetStatusToEnabled()
    {
        var dict = CreateDictionary();
        dict.Disable();

        dict.Enable();

        dict.Status.Should().Be(DictionaryStatus.Enabled);
    }

    [Fact]
    public void Disable_ShouldSetStatusToDisabled()
    {
        var dict = CreateDictionary();

        dict.Disable();

        dict.Status.Should().Be(DictionaryStatus.Disabled);
    }

    #endregion

    #region AddItem

    [Fact]
    public void AddItem_WithValidParameters_ShouldAddItemToCollection()
    {
        var dict = CreateDictionary();

        dict.AddItem(ValidItemId, ValidItemCode, ValidItemLabel, ValidItemValue, sortOrder: 1);

        dict.Items.Should().ContainSingle();
        var item = dict.Items.First();
        item.Id.Should().Be(ValidItemId);
        item.Code.Should().Be(ValidItemCode);
        item.Label.Should().Be(ValidItemLabel);
        item.Value.Should().Be(ValidItemValue);
        item.SortOrder.Should().Be(1);
        item.DictionaryId.Should().Be(dict.Id);
        item.Status.Should().Be(DictionaryStatus.Enabled);
    }

    [Fact]
    public void AddItem_MultipleItems_ShouldAllBeAdded()
    {
        var dict = CreateDictionary();

        dict.AddItem(Guid.NewGuid(), "MALE", "Male", "1", 1);
        dict.AddItem(Guid.NewGuid(), "FEMALE", "Female", "2", 2);

        dict.Items.Should().HaveCount(2);
    }

    [Fact]
    public void AddItem_WithEmptyItemId_ShouldThrowDictItemIdEmpty()
    {
        var dict = CreateDictionary();

        var act = () => dict.AddItem(Guid.Empty, ValidItemCode, ValidItemLabel, ValidItemValue, 1);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_ITEM_ID_EMPTY");
    }

    [Fact]
    public void AddItem_WithDuplicateCode_ShouldThrowDictItemCodeDuplicate()
    {
        var dict = CreateDictionary();
        dict.AddItem(ValidItemId, ValidItemCode, ValidItemLabel, ValidItemValue, 1);

        var act = () => dict.AddItem(Guid.NewGuid(), ValidItemCode, "Another Label", "2", 2);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_ITEM_CODE_DUPLICATE");
    }

    [Fact]
    public void AddItem_WithDuplicateCodeCaseInsensitive_ShouldThrowDictItemCodeDuplicate()
    {
        var dict = CreateDictionary();
        dict.AddItem(ValidItemId, "MALE", "Male", "1", 1);

        var act = () => dict.AddItem(Guid.NewGuid(), "male", "Male Lower", "2", 2);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_ITEM_CODE_DUPLICATE");
    }

    #endregion

    #region RemoveItem

    [Fact]
    public void RemoveItem_ExistingItem_ShouldRemoveFromCollection()
    {
        var dict = CreateDictionary();
        dict.AddItem(ValidItemId, ValidItemCode, ValidItemLabel, ValidItemValue, 1);

        dict.RemoveItem(ValidItemId);

        dict.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_NonExistingItem_ShouldNotThrow()
    {
        var dict = CreateDictionary();
        dict.AddItem(ValidItemId, ValidItemCode, ValidItemLabel, ValidItemValue, 1);

        var act = () => dict.RemoveItem(Guid.NewGuid());

        act.Should().NotThrow();
        dict.Items.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveItem_FromEmptyCollection_ShouldNotThrow()
    {
        var dict = CreateDictionary();

        var act = () => dict.RemoveItem(Guid.NewGuid());

        act.Should().NotThrow();
    }

    #endregion

    #region UpdateItem

    [Fact]
    public void UpdateItem_ExistingItem_ShouldUpdateProperties()
    {
        var dict = CreateDictionary();
        dict.AddItem(ValidItemId, ValidItemCode, ValidItemLabel, ValidItemValue, 1);

        dict.UpdateItem(ValidItemId, "Updated Label", "99", 5);

        var item = dict.Items.First();
        item.Label.Should().Be("Updated Label");
        item.Value.Should().Be("99");
        item.SortOrder.Should().Be(5);
    }

    [Fact]
    public void UpdateItem_NonExistingItem_ShouldThrowDictItemNotFound()
    {
        var dict = CreateDictionary();

        var act = () => dict.UpdateItem(Guid.NewGuid(), "Label", "Value", 1);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("DICT_ITEM_NOT_FOUND");
    }

    #endregion

    private static DataDictionary CreateDictionary()
    {
        return DataDictionary.Create(ValidDictionaryId, ValidCode, ValidName, ValidDescription);
    }
}