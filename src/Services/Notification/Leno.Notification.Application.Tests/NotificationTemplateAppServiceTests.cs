using Leno.Notification.Application.DTOs;
using Leno.Notification.Application.Services;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Notification.Application.Tests;

/// <summary>
/// P0-12 修复验证：按 ID 查询模板应走主键查询（INotificationTemplateRepository.GetByIdAsync），
/// 而非调用 QueryTemplatesAsync(null, null, 1, int.MaxValue) 全表加载后内存 FirstOrDefault。
/// </summary>
public class NotificationTemplateAppServiceTests
{
    private readonly Mock<INotificationTemplateRepository> _templateRepoMock;
    private readonly Mock<ITemplateRenderer> _rendererMock;
    private readonly Mock<ITemplateRenderService> _templateRenderServiceMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<NotificationTemplateAppService>> _loggerMock;
    private readonly NotificationTemplateAppService _sut;

    public NotificationTemplateAppServiceTests()
    {
        _templateRepoMock = new Mock<INotificationTemplateRepository>(MockBehavior.Strict);
        _rendererMock = new Mock<ITemplateRenderer>(MockBehavior.Strict);
        _templateRenderServiceMock = new Mock<ITemplateRenderService>(MockBehavior.Strict);
        // IUnitOfWork 继承 IDisposable，使用 Loose 避免 Dispose 未设置抛异常
        _uowMock = new Mock<IUnitOfWork>(MockBehavior.Loose);
        _loggerMock = new Mock<ILogger<NotificationTemplateAppService>>();

        _sut = new NotificationTemplateAppService(
            _templateRepoMock.Object,
            _rendererMock.Object,
            _templateRenderServiceMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    private static NotificationTemplate BuildTemplate(Guid templateId)
    {
        return NotificationTemplate.Create(
            templateId,
            "test_code",
            "Test Template",
            NotificationChannel.Sms,
            "Test Subject",
            "Test Body",
            new List<TemplateVariable>(),
            "SMS_12345678");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldQueryByPrimaryKey_NotLoadAll()
    {
        // Arrange — 修复前：调用 QueryTemplatesAsync(null, null, 1, int.MaxValue) 全表加载；
        // 修复后：调用 INotificationTemplateRepository.GetByIdAsync 走主键查询。
        var templateId = Guid.NewGuid();
        var template = BuildTemplate(templateId);

        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(templateId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.TemplateId.Should().Be(templateId);
        result.Code.Should().Be("test_code");
        result.Channel.Should().Be(NotificationChannel.Sms);

        _templateRepoMock.Verify(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
        // 修复后：不应再调用 QueryAsync 全表加载
        _templateRepoMock.Verify(
            r => r.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationChannel?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        // GetByIdAsync 为只读查询，不应触发 SaveChanges
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentTemplate_ShouldReturnNull()
    {
        // Arrange — 模板不存在时仓储返回 null，应用服务应返回 null（而非抛异常）
        var templateId = Guid.NewGuid();

        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationTemplate?)null);

        // Act
        var result = await _sut.GetByIdAsync(templateId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _templateRepoMock.Verify(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
        _templateRepoMock.Verify(
            r => r.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationChannel?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_EmptyTemplateId_ShouldThrowArgumentException()
    {
        // Arrange — 空标识应直接抛 ArgumentException，不应查询仓储

        // Act
        var act = () => _sut.GetByIdAsync(Guid.Empty, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.ParamName.Should().Be("templateId");

        _templateRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _templateRepoMock.Verify(
            r => r.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationChannel?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldMapAllFieldsToDto()
    {
        // Arrange — 验证 DTO 字段映射完整，避免遗漏 SmsTemplateCode 等关键字段
        var templateId = Guid.NewGuid();
        var template = NotificationTemplate.Create(
            templateId,
            "order_created",
            "订单创建通知",
            NotificationChannel.Sms,
            "您的订单已创建",
            "订单号 {{orderId}} 已提交",
            new List<TemplateVariable>
            {
                TemplateVariable.Create("orderId", false, "订单号")
            },
            "SMS_87654321",
            "订单创建模板",
            Guid.NewGuid());

        _templateRepoMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(templateId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.TemplateId.Should().Be(templateId);
        result.Code.Should().Be("order_created");
        result.Name.Should().Be("订单创建通知");
        result.Channel.Should().Be(NotificationChannel.Sms);
        result.Subject.Should().Be("您的订单已创建");
        result.Body.Should().Be("订单号 {{orderId}} 已提交");
        result.SmsTemplateCode.Should().Be("SMS_87654321");
        result.Description.Should().Be("订单创建模板");
        result.Variables.Should().HaveCount(1);
        result.Status.Should().Be(TemplateStatus.Enabled);

        _templateRepoMock.Verify(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
