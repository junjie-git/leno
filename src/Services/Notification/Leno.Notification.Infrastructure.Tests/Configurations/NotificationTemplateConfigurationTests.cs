using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Leno.Notification.Infrastructure.Tests.Configurations;

public class NotificationTemplateConfigurationTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: "notification_template_test_" + Guid.NewGuid())
            .Options;
        using var context = new NotificationDbContext(options);
        return context.Model;
    }

    [Fact]
    public void NotificationTemplate_CodeChannelIndex_ShouldBeUnique()
    {
        // Arrange
        var model = BuildModel();
        var entityType = model.FindEntityType(typeof(NotificationTemplate));
        entityType.Should().NotBeNull("NotificationTemplate 必须在 DbContext 模型中注册");

        // Act — 查找 (Code, Channel) 索引
        var index = entityType!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 2
                && i.Properties.Any(p => p.Name == nameof(NotificationTemplate.Code))
                && i.Properties.Any(p => p.Name == nameof(NotificationTemplate.Channel)));

        // Assert — 修复后：索引应声明 IsUnique() = true
        index.Should().NotBeNull("应存在 (Code, Channel) 复合索引");
        index!.IsUnique.Should().BeTrue("(Code, Channel) 索引必须为唯一约束，防止同一 code+channel 存在多个 Enabled 模板");
    }

    [Fact]
    public void NotificationTemplate_CodeChannelIndex_ShouldHaveExpectedDatabaseName()
    {
        // Arrange
        var model = BuildModel();
        var entityType = model.FindEntityType(typeof(NotificationTemplate));

        // Act
        var index = entityType!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 2
                && i.Properties.Any(p => p.Name == nameof(NotificationTemplate.Code))
                && i.Properties.Any(p => p.Name == nameof(NotificationTemplate.Channel)));

        // Assert — 索引名固定以便 Migration 与运维检索
        index.Should().NotBeNull();
        index!.GetDatabaseName().Should().Be("ix_notification_templates_code_channel");
    }
}
