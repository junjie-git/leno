using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests.Services;

/// <summary>
/// H-02 验证 AnnouncementAppService 不再依赖 IEventBus，
/// 仅通过 SaveEntitiesAsync 走发件箱投递集成事件。
/// </summary>
public sealed class AnnouncementAppServiceOutboxTests
{
    private readonly Mock<ISystemAnnouncementRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AnnouncementAppService _service;

    public AnnouncementAppServiceOutboxTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new AnnouncementAppService(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<AnnouncementAppService>.Instance);
    }

    [Fact]
    public async Task PublishAsync_Should_Call_Only_SaveEntitiesAsync_Without_Manual_Publish()
    {
        var announcementId = Guid.NewGuid();
        var existing = SystemAnnouncement.Create(
            announcementId, "标题", "内容", AnnouncementType.System,
            AnnouncementTargetAudience.All, null, null);
        _repoMock.Setup(r => r.GetByIdAsync(announcementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _service.PublishAsync(announcementId, CancellationToken.None);

        existing.Status.Should().Be(AnnouncementStatus.Published);
        _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
