using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Exceptions;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Services;

namespace Leno.Notification.Application.Tests;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    private static NotificationTemplate CreateTestTemplate(
        string subject = "Hello {{UserName}}",
        string body = "<p>Your order {{OrderId}} has been {{Status}}.</p>",
        List<TemplateVariable>? variables = null)
    {
        var template = NotificationTemplate.Create(
            Guid.NewGuid(),
            "TestTemplate",
            "Test Template",
            NotificationChannel.Email,
            subject,
            body,
            variables ?? new List<TemplateVariable>
            {
                TemplateVariable.Create("UserName", required: true),
                TemplateVariable.Create("OrderId", required: true),
                TemplateVariable.Create("Status", required: false)
            });

        return template;
    }

    #region RenderAsync

    [Fact]
    public async Task RenderAsync_AllRequiredVariables_ShouldRenderSuccessfully()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John",
            ["OrderId"] = "ORD-12345",
            ["Status"] = "Shipped"
        };

        // Act
        var result = await _renderer.RenderAsync(template, variables);

        // Assert
        result.Title.Should().Be("Hello John");
        result.Content.Should().Be("<p>Your order ORD-12345 has been Shipped.</p>");
        result.ContentSnapshot.Should().NotBeNullOrEmpty();
        result.ContentSnapshot.Should().Contain("TestTemplate");
        result.ContentSnapshot.Should().Contain("John");
        result.ContentSnapshot.Should().Contain("ORD-12345");
    }

    [Fact]
    public async Task RenderAsync_OptionalVariableMissing_ShouldPreservePlaceholder()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John",
            ["OrderId"] = "ORD-12345"
            // Status is optional and not provided
        };

        // Act
        var result = await _renderer.RenderAsync(template, variables);

        // Assert
        result.Title.Should().Be("Hello John");
        result.Content.Should().Be("<p>Your order ORD-12345 has been {{Status}}.</p>");
    }

    [Fact]
    public async Task RenderAsync_RequiredVariableMissing_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John"
            // OrderId is required but missing
        };

        // Act
        var act = async () => await _renderer.RenderAsync(template, variables);

        // Assert
        await act.Should().ThrowAsync<NotificationDomainException>()
            .Where(ex => ex.ErrorCode == "TEMPLATE_REQUIRED_VARIABLE_MISSING");
    }

    [Fact]
    public async Task RenderAsync_RequiredVariableEmpty_ShouldThrowNotificationDomainException()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John",
            ["OrderId"] = "" // Required but empty
        };

        // Act
        var act = async () => await _renderer.RenderAsync(template, variables);

        // Assert
        await act.Should().ThrowAsync<NotificationDomainException>()
            .Where(ex => ex.ErrorCode == "TEMPLATE_REQUIRED_VARIABLE_MISSING");
    }

    [Fact]
    public async Task RenderAsync_HtmlSpecialCharacters_ShouldBeEscaped()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John",
            ["OrderId"] = "ORD-12345",
            ["Status"] = "<script>alert('xss')</script>"
        };

        // Act
        var result = await _renderer.RenderAsync(template, variables);

        // Assert
        result.Content.Should().NotContain("<script>");
        result.Content.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public async Task RenderAsync_TitleNotHtmlEscaped_ShouldKeepRawText()
    {
        // Arrange
        var template = CreateTestTemplate(subject: "Hello {{UserName}} & Welcome");
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John & Jane",
            ["OrderId"] = "ORD-12345"
        };

        // Act
        var result = await _renderer.RenderAsync(template, variables);

        // Assert
        result.Title.Should().Be("Hello John & Jane & Welcome");
    }

    [Fact]
    public async Task RenderAsync_NullTemplate_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _renderer.RenderAsync(null!, new Dictionary<string, string>());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RenderAsync_NullVariables_ShouldThrowArgumentNullException()
    {
        // Arrange
        var template = CreateTestTemplate();

        // Act
        var act = async () => await _renderer.RenderAsync(template, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RenderAsync_ContentSnapshot_ShouldContainTemplateCode()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John",
            ["OrderId"] = "ORD-12345"
        };

        // Act
        var result = await _renderer.RenderAsync(template, variables);

        // Assert
        result.ContentSnapshot.Should().Contain("\"templateCode\"");
        result.ContentSnapshot.Should().Contain("TestTemplate");
        result.ContentSnapshot.Should().Contain("\"renderedAt\"");
        result.ContentSnapshot.Should().Contain("\"title\"");
        result.ContentSnapshot.Should().Contain("\"content\"");
        result.ContentSnapshot.Should().Contain("\"variables\"");
    }

    #endregion

    #region ValidateUndefinedPlaceholders

    [Fact]
    public void ValidateUndefinedPlaceholders_AllDefined_ShouldReturnEmpty()
    {
        // Arrange
        var template = CreateTestTemplate();

        // Act
        var undefined = _renderer.ValidateUndefinedPlaceholders(template);

        // Assert
        undefined.Should().BeEmpty();
    }

    [Fact]
    public void ValidateUndefinedPlaceholders_UndefinedInBody_ShouldReturnPlaceholderName()
    {
        // Arrange
        var template = CreateTestTemplate(
            body: "Hello {{UserName}}, your {{UndefinedVar}} is ready.",
            variables: new List<TemplateVariable>
            {
                TemplateVariable.Create("UserName", required: true)
            });

        // Act
        var undefined = _renderer.ValidateUndefinedPlaceholders(template);

        // Assert
        undefined.Should().Contain("UndefinedVar");
        undefined.Should().HaveCount(1);
    }

    [Fact]
    public void ValidateUndefinedPlaceholders_UndefinedInSubject_ShouldReturnPlaceholderName()
    {
        // Arrange
        var template = CreateTestTemplate(
            subject: "{{UndefinedSubject}} Notification",
            variables: new List<TemplateVariable>
            {
                TemplateVariable.Create("UserName", required: true)
            });

        // Act
        var undefined = _renderer.ValidateUndefinedPlaceholders(template);

        // Assert
        undefined.Should().Contain("UndefinedSubject");
    }

    [Fact]
    public void ValidateUndefinedPlaceholders_MultipleUndefined_ShouldReturnAll()
    {
        // Arrange
        var template = CreateTestTemplate(
            subject: "{{VarA}} {{VarB}}",
            body: "{{VarC}} and {{VarD}}",
            variables: new List<TemplateVariable>
            {
                TemplateVariable.Create("VarA", required: true)
            });

        // Act
        var undefined = _renderer.ValidateUndefinedPlaceholders(template);

        // Assert
        undefined.Should().Contain("VarB");
        undefined.Should().Contain("VarC");
        undefined.Should().Contain("VarD");
        undefined.Should().NotContain("VarA");
        undefined.Should().HaveCount(3);
    }

    [Fact]
    public void ValidateUndefinedPlaceholders_NoPlaceholders_ShouldReturnEmpty()
    {
        // Arrange
        var template = CreateTestTemplate(
            subject: "Plain Subject",
            body: "Plain body with no placeholders.",
            variables: new List<TemplateVariable>());

        // Act
        var undefined = _renderer.ValidateUndefinedPlaceholders(template);

        // Assert
        undefined.Should().BeEmpty();
    }

    #endregion

    #region Render (sync)

    [Fact]
    public void Render_ValidVariables_ShouldReturnTitleAndContent()
    {
        // Arrange
        var template = CreateTestTemplate();
        var variables = new Dictionary<string, string>
        {
            ["UserName"] = "John",
            ["OrderId"] = "ORD-12345",
            ["Status"] = "Shipped"
        };

        // Act
        var (title, content) = _renderer.Render(template, variables);

        // Assert
        title.Should().Be("Hello John");
        content.Should().Be("<p>Your order ORD-12345 has been Shipped.</p>");
    }

    #endregion
}