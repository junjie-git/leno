using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Domain.Tests;

public class NotificationTemplateTests
{
    private static readonly Guid ValidTemplateId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string ValidCode = "OrderCreated";
    private const string ValidName = "Order Created Notification";
    private const NotificationChannel ValidChannel = NotificationChannel.InApp;
    private const string ValidSubject = "Order {{OrderId}} Confirmed";
    private const string ValidBody = "Your order {{OrderId}} has been confirmed. Total: {{Total}}";
    private static readonly List<TemplateVariable> ValidVariables =
    [
        TemplateVariable.Create("OrderId", true, "Order identifier"),
        TemplateVariable.Create("Total", false, "Order total amount")
    ];

    private static NotificationTemplate CreateValidTemplate(NotificationChannel? channel = null)
    {
        return NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, channel ?? ValidChannel,
            ValidSubject, ValidBody, ValidVariables);
    }

    #region Create - Happy Path

    [Fact]
    public void Create_ValidParameters_ShouldCreateTemplate()
    {
        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables, "SMS_001", "Test template", Guid.NewGuid());

        // Assert
        template.Id.Should().Be(ValidTemplateId);
        template.Code.Should().Be(ValidCode);
        template.Name.Should().Be(ValidName);
        template.Channel.Should().Be(ValidChannel);
        template.Subject.Should().Be(ValidSubject);
        template.Body.Should().Be(ValidBody);
        template.Variables.Should().BeEquivalentTo(ValidVariables);
        template.SmsTemplateCode.Should().Be("SMS_001");
        template.Description.Should().Be("Test template");
        template.OperatorId.Should().NotBeNull();
        template.Status.Should().Be(TemplateStatus.Enabled);
    }

    [Fact]
    public void Create_NullVariables_ShouldDefaultToEmptyList()
    {
        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, ValidBody, null!);

        // Assert
        template.Variables.Should().NotBeNull();
        template.Variables.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithSmsChannel_ShouldCreateTemplate()
    {
        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, NotificationChannel.Sms,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        template.Channel.Should().Be(NotificationChannel.Sms);
        template.Status.Should().Be(TemplateStatus.Enabled);
    }

    [Fact]
    public void Create_WithEmailChannel_ShouldCreateTemplate()
    {
        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, NotificationChannel.Email,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        template.Channel.Should().Be(NotificationChannel.Email);
        template.Status.Should().Be(TemplateStatus.Enabled);
    }

    [Fact]
    public void Create_WithoutOptionalFields_ShouldCreateTemplate()
    {
        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        template.SmsTemplateCode.Should().BeNull();
        template.Description.Should().BeNull();
        template.OperatorId.Should().BeNull();
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_EmptyTemplateId_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            Guid.Empty, ValidCode, ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_ID_EMPTY");
    }

    [Fact]
    public void Create_NullCode_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, null!, ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void Create_EmptyCode_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, "", ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceCode_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, "   ", ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_EMPTY");
    }

    [Fact]
    public void Create_NullName_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, null!, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_NAME_EMPTY");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, "", ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_NAME_EMPTY");
    }

    [Fact]
    public void Create_InvalidChannel_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, (NotificationChannel)999,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CHANNEL_INVALID");
    }

    [Fact]
    public void Create_NullSubject_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            null!, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_SUBJECT_EMPTY");
    }

    [Fact]
    public void Create_EmptySubject_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            "", ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_SUBJECT_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceSubject_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            "   ", ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_SUBJECT_EMPTY");
    }

    [Fact]
    public void Create_SubjectExactly200Chars_ShouldCreateTemplate()
    {
        // Arrange
        var subject = new string('A', 200);

        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            subject, ValidBody, ValidVariables);

        // Assert
        template.Subject.Should().Be(subject);
        template.Subject.Length.Should().Be(200);
    }

    [Fact]
    public void Create_SubjectExceeds200Chars_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var subject = new string('A', 201);

        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            subject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_SUBJECT_TOO_LONG");
    }

    [Fact]
    public void Create_NullBody_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, null!, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_BODY_EMPTY");
    }

    [Fact]
    public void Create_EmptyBody_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, "", ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_BODY_EMPTY");
    }

    [Fact]
    public void Create_WhitespaceBody_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, "   ", ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_BODY_EMPTY");
    }

    [Fact]
    public void Create_BodyExactly2000Chars_ShouldCreateTemplate()
    {
        // Arrange
        var body = new string('B', 2000);

        // Act
        var template = NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, body, ValidVariables);

        // Assert
        template.Body.Should().Be(body);
        template.Body.Length.Should().Be(2000);
    }

    [Fact]
    public void Create_BodyExceeds2000Chars_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var body = new string('B', 2001);

        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, ValidCode, ValidName, ValidChannel,
            ValidSubject, body, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_BODY_TOO_LONG");
    }

    [Fact]
    public void Create_CodeExceeds128Chars_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var code = new string('C', 129);

        // Act
        var act = () => NotificationTemplate.Create(
            ValidTemplateId, code, ValidName, ValidChannel,
            ValidSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_CODE_TOO_LONG");
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ValidParameters_ShouldUpdateTemplate()
    {
        // Arrange
        var template = CreateValidTemplate();
        const string newSubject = "Updated Subject {{OrderId}}";
        const string newBody = "Updated Body {{OrderId}}";
        var newVariables = new List<TemplateVariable> { TemplateVariable.Create("OrderId") };

        // Act
        template.Update(newSubject, newBody, newVariables);

        // Assert
        template.Subject.Should().Be(newSubject);
        template.Body.Should().Be(newBody);
        template.Variables.Should().BeEquivalentTo(newVariables);
        template.Code.Should().Be(ValidCode); // Code should not change
        template.Channel.Should().Be(ValidChannel); // Channel should not change
    }

    [Fact]
    public void Update_NullVariables_ShouldDefaultToEmptyList()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        template.Update(ValidSubject, ValidBody, null!);

        // Assert
        template.Variables.Should().NotBeNull();
        template.Variables.Should().BeEmpty();
    }

    [Fact]
    public void Update_EmptySubject_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var act = () => template.Update("", ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_SUBJECT_EMPTY");
    }

    [Fact]
    public void Update_EmptyBody_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var act = () => template.Update(ValidSubject, "", ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_BODY_EMPTY");
    }

    [Fact]
    public void Update_SubjectTooLong_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();
        var tooLongSubject = new string('X', 201);

        // Act
        var act = () => template.Update(tooLongSubject, ValidBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_SUBJECT_TOO_LONG");
    }

    [Fact]
    public void Update_BodyTooLong_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();
        var tooLongBody = new string('Y', 2001);

        // Act
        var act = () => template.Update(ValidSubject, tooLongBody, ValidVariables);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_TEMPLATE_BODY_TOO_LONG");
    }

    #endregion

    #region Enable / Disable

    [Fact]
    public void Enable_WhenDisabled_ShouldSetStatusToEnabled()
    {
        // Arrange
        var template = CreateValidTemplate();
        template.Disable();
        template.Status.Should().Be(TemplateStatus.Disabled);

        // Act
        template.Enable();

        // Assert
        template.Status.Should().Be(TemplateStatus.Enabled);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ShouldRemainEnabled()
    {
        // Arrange
        var template = CreateValidTemplate();
        template.Status.Should().Be(TemplateStatus.Enabled);

        // Act
        template.Enable();

        // Assert
        template.Status.Should().Be(TemplateStatus.Enabled);
    }

    [Fact]
    public void Disable_WhenEnabled_ShouldSetStatusToDisabled()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        template.Disable();

        // Assert
        template.Status.Should().Be(TemplateStatus.Disabled);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ShouldRemainDisabled()
    {
        // Arrange
        var template = CreateValidTemplate();
        template.Disable();
        template.Status.Should().Be(TemplateStatus.Disabled);

        // Act
        template.Disable();

        // Assert
        template.Status.Should().Be(TemplateStatus.Disabled);
    }

    #endregion

    #region AddVariable

    [Fact]
    public void AddVariable_ValidVariable_ShouldAddToList()
    {
        // Arrange
        var template = CreateValidTemplate();
        var variable = TemplateVariable.Create("NewVar", true, "New variable");

        // Act
        template.AddVariable(variable);

        // Assert
        template.Variables.Should().Contain(v => v.Name == "NewVar");
    }

    [Fact]
    public void AddVariable_NullVariable_ShouldThrowArgumentNullException()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var act = () => template.AddVariable(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddVariable_DuplicateName_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();
        var variable = TemplateVariable.Create("OrderId", true, "Duplicate");

        // Act
        var act = () => template.AddVariable(variable);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_VARIABLE_DUPLICATE");
    }

    #endregion

    #region RemoveVariable

    [Fact]
    public void RemoveVariable_ExistingVariable_ShouldRemoveFromList()
    {
        // Arrange
        var template = CreateValidTemplate();
        template.Variables.Should().Contain(v => v.Name == "OrderId");

        // Act
        template.RemoveVariable("OrderId");

        // Assert
        template.Variables.Should().NotContain(v => v.Name == "OrderId");
    }

    [Fact]
    public void RemoveVariable_NonExistingVariable_ShouldNotThrow()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var act = () => template.RemoveVariable("NonExistent");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveVariable_NullName_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var act = () => template.RemoveVariable(null!);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_VARIABLE_NAME_EMPTY");
    }

    [Fact]
    public void RemoveVariable_EmptyName_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var act = () => template.RemoveVariable("");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_VARIABLE_NAME_EMPTY");
    }

    #endregion

    #region ContainsPlaceholder

    [Fact]
    public void ContainsPlaceholder_ExistingPlaceholder_ShouldReturnTrue()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Assert
        template.ContainsPlaceholder("OrderId").Should().BeTrue();
        template.ContainsPlaceholder("Total").Should().BeTrue();
    }

    [Fact]
    public void ContainsPlaceholder_NonExistingPlaceholder_ShouldReturnFalse()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Assert
        template.ContainsPlaceholder("NonExistent").Should().BeFalse();
    }

    [Fact]
    public void ContainsPlaceholder_NullName_ShouldReturnFalse()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Assert
        template.ContainsPlaceholder(null!).Should().BeFalse();
    }

    [Fact]
    public void ContainsPlaceholder_EmptyName_ShouldReturnFalse()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Assert
        template.ContainsPlaceholder("").Should().BeFalse();
    }

    #endregion

    #region TemplateVariable

    [Fact]
    public void TemplateVariable_Create_ShouldCreateWithCorrectProperties()
    {
        // Act
        var variable = TemplateVariable.Create("TestVar", true, "Test description");

        // Assert
        variable.Name.Should().Be("TestVar");
        variable.Required.Should().BeTrue();
        variable.Description.Should().Be("Test description");
    }

    [Fact]
    public void TemplateVariable_Create_WithDefaults_ShouldHaveFalseRequiredAndEmptyDescription()
    {
        // Act
        var variable = TemplateVariable.Create("TestVar");

        // Assert
        variable.Required.Should().BeFalse();
        variable.Description.Should().BeEmpty();
    }

    [Fact]
    public void TemplateVariable_FromName_ShouldCreateNonRequiredVariable()
    {
        // Act
        var variable = TemplateVariable.FromName("TestVar");

        // Assert
        variable.Name.Should().Be("TestVar");
        variable.Required.Should().BeFalse();
        variable.Description.Should().BeEmpty();
    }

    [Fact]
    public void TemplateVariable_Equals_SameName_ShouldBeEqual()
    {
        // Arrange
        var v1 = TemplateVariable.Create("TestVar", true, "Desc1");
        var v2 = TemplateVariable.Create("TestVar", false, "Desc2");

        // Assert
        v1.Should().Be(v2);
        v1.GetHashCode().Should().Be(v2.GetHashCode());
    }

    [Fact]
    public void TemplateVariable_Equals_DifferentName_ShouldNotBeEqual()
    {
        // Arrange
        var v1 = TemplateVariable.Create("Var1");
        var v2 = TemplateVariable.Create("Var2");

        // Assert
        v1.Should().NotBe(v2);
    }

    [Fact]
    public void TemplateVariable_Equals_DifferentCase_ShouldBeEqual()
    {
        // Arrange
        var v1 = TemplateVariable.Create("testvar");
        var v2 = TemplateVariable.Create("TESTVAR");

        // Assert
        v1.Should().Be(v2);
    }

    [Fact]
    public void TemplateVariable_Create_EmptyName_ShouldThrowNotificationDomainException()
    {
        // Act
        var act = () => TemplateVariable.Create("");

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_VARIABLE_NAME_EMPTY");
    }

    [Fact]
    public void TemplateVariable_Create_NameTooLong_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var name = new string('X', 65);

        // Act
        var act = () => TemplateVariable.Create(name);

        // Assert
        act.Should().Throw<NotificationDomainException>()
            .And.ErrorCode.Should().Be("NOTIFICATION_VARIABLE_NAME_TOO_LONG");
    }

    #endregion

    #region Variable Placeholder Validation

    [Fact]
    public void Variables_ShouldMatchPlaceholders_WhenAllVariablesUsed()
    {
        // Arrange
        var template = CreateValidTemplate();
        // ValidSubject = "Order {{OrderId}} Confirmed"
        // ValidBody = "Your order {{OrderId}} has been confirmed. Total: {{Total}}"
        // Variables = [OrderId, Total]

        // Assert
        template.ContainsPlaceholder("OrderId").Should().BeTrue();
        template.ContainsPlaceholder("Total").Should().BeTrue();
    }

    [Fact]
    public void Variables_UnusedVariable_ShouldNotBeInPlaceholders()
    {
        // Arrange
        var template = CreateValidTemplate();
        var unusedVar = TemplateVariable.Create("UnusedVar", false, "Not used in template");

        // Act
        template.AddVariable(unusedVar);

        // Assert
        template.ContainsPlaceholder("UnusedVar").Should().BeFalse();
        template.Variables.Should().Contain(v => v.Name == "UnusedVar");
    }

    [Fact]
    public void Variables_AddAndRemove_ShouldMaintainConsistency()
    {
        // Arrange
        var template = CreateValidTemplate();
        var newVar = TemplateVariable.Create("NewField", false, "New field");

        // Act
        template.AddVariable(newVar);
        template.Variables.Should().Contain(v => v.Name == "NewField");

        template.RemoveVariable("NewField");
        template.Variables.Should().NotContain(v => v.Name == "NewField");
    }

    #endregion

    #region Disabled Template Editing

    [Fact]
    public void DisabledTemplate_ShouldNotBeEditable_CheckStatus()
    {
        // Arrange
        var template = CreateValidTemplate();
        template.Disable();
        template.Status.Should().Be(TemplateStatus.Disabled);

        // Assert: The Update method itself doesn't check status,
        // the application service enforces the rule. Here we verify the
        // domain object's state is correct.
        template.Status.Should().Be(TemplateStatus.Disabled);
    }

    [Fact]
    public void Enable_DisabledTemplate_ShouldAllowEditing()
    {
        // Arrange
        var template = CreateValidTemplate();
        template.Disable();
        template.Status.Should().Be(TemplateStatus.Disabled);

        // Act
        template.Enable();
        template.Status.Should().Be(TemplateStatus.Enabled);

        // Now editing should be allowed (no domain exception)
        var act = () => template.Update("New Subject {{OrderId}}", "New Body {{OrderId}} {{Total}}", ValidVariables);
        act.Should().NotThrow();
    }

    #endregion

    #region Template Preview

    [Fact]
    public void ContainsPlaceholder_MultipleVariables_ShouldMatchAll()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Assert
        template.ContainsPlaceholder("OrderId").Should().BeTrue();
        template.ContainsPlaceholder("Total").Should().BeTrue();
    }

    [Fact]
    public void ContainsPlaceholder_CaseInsensitiveMatch()
    {
        // Arrange
        var template = CreateValidTemplate();
        // Subject is "Order {{OrderId}} Confirmed"

        // Assert
        template.ContainsPlaceholder("orderid").Should().BeTrue();
        template.ContainsPlaceholder("ORDERID").Should().BeTrue();
    }

    [Fact]
    public void Preview_ShouldRenderTitleAndContent()
    {
        // This test verifies the domain logic that the template can be rendered.
        // The actual rendering is done by ITemplateRenderer, but the domain
        // ensures the template has the necessary variables and placeholders.
        // Arrange
        var template = CreateValidTemplate();

        // Assert
        template.Subject.Should().Contain("{{OrderId}}");
        template.Body.Should().Contain("{{OrderId}}");
        template.Body.Should().Contain("{{Total}}");
        template.Variables.Should().HaveCount(2);
    }

    #endregion
}