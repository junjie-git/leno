using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Application.Services;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;

namespace Leno.UserAuth.Application.Tests;

public class NotificationPreferencesAppServiceTests
{
    private readonly Mock<INotificationPreferencesRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly NotificationPreferencesAppService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationPreferencesAppServiceTests()
    {
        _sut = new NotificationPreferencesAppService(_repoMock.Object, _uowMock.Object);
    }

    #region GetAsync

    [Fact]
    public async Task GetAsync_ExistingPreferences_ShouldReturnDto()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var result = await _sut.GetAsync(_userId);

        result.UserId.Should().Be(_userId);
        result.Preferences.Should().HaveCount(7);
        result.DndEnabled.Should().BeFalse();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<NotificationPreferences>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_FirstAccess_ShouldLazyInitAndPersist()
    {
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var result = await _sut.GetAsync(_userId);

        result.UserId.Should().Be(_userId);
        result.Preferences.Should().HaveCount(7);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<NotificationPreferences>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateAsync - Single Channel Mode

    [Fact]
    public async Task UpdateAsync_SingleChannel_ShouldUpdateSpecifiedChannel()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            EventType = NotificationEventType.OrderStatus,
            Channel = NotificationChannel.Sms,
            Enabled = true
        };

        var result = await _sut.UpdateAsync(_userId, request);

        var orderItem = result.Preferences.First(p => p.EventType == NotificationEventType.OrderStatus);
        orderItem.Channels.Sms.Should().BeTrue();
        _repoMock.Verify(r => r.UpdateAsync(preferences, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DisableInApp_ShouldThrowException()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            EventType = NotificationEventType.OrderStatus,
            Channel = NotificationChannel.InApp,
            Enabled = false
        };

        var act = () => _sut.UpdateAsync(_userId, request);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*站内信*不可关闭*");
    }

    [Fact]
    public async Task UpdateAsync_PartialFields_ShouldThrowValidationException()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            EventType = NotificationEventType.OrderStatus,
            Channel = null,
            Enabled = true
        };

        var act = () => _sut.UpdateAsync(_userId, request);

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    #endregion

    #region UpdateAsync - Batch Mode

    [Fact]
    public async Task UpdateAsync_BatchSettings_ShouldReplaceAll()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            BatchSettings = new List<BatchNotificationPreferenceSetting>
            {
                new() { EventType = NotificationEventType.OrderStatus, Channel = NotificationChannel.Sms, Enabled = true },
                new() { EventType = NotificationEventType.SystemNotice, Channel = NotificationChannel.Email, Enabled = true }
            }
        };

        var result = await _sut.UpdateAsync(_userId, request);

        var orderItem = result.Preferences.First(p => p.EventType == NotificationEventType.OrderStatus);
        orderItem.Channels.Sms.Should().BeTrue();
        var systemItem = result.Preferences.First(p => p.EventType == NotificationEventType.SystemNotice);
        systemItem.Channels.Email.Should().BeTrue();
    }

    #endregion

    #region UpdateAsync - DnD

    [Fact]
    public async Task UpdateAsync_EnableDnd_ShouldSetTimes()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            DndEnabled = true,
            DndStart = "22:00",
            DndEnd = "08:00"
        };

        var result = await _sut.UpdateAsync(_userId, request);

        result.DndEnabled.Should().BeTrue();
        result.DndStart.Should().Be("22:00");
        result.DndEnd.Should().Be("08:00");
    }

    [Fact]
    public async Task UpdateAsync_EnableDndWithoutTimes_ShouldThrowException()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            DndEnabled = true,
            DndStart = null,
            DndEnd = null
        };

        var act = () => _sut.UpdateAsync(_userId, request);

        await act.Should().ThrowAsync<UserAuthDomainException>()
            .WithMessage("*免打扰时必须同时提供起止时间*");
    }

    [Fact]
    public async Task UpdateAsync_InvalidDndTimeFormat_ShouldThrowValidationException()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            DndEnabled = true,
            DndStart = "invalid",
            DndEnd = "08:00"
        };

        var act = () => _sut.UpdateAsync(_userId, request);

        await act.Should().ThrowAsync<UserAuthValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_DisableDnd_ShouldClearTimes()
    {
        var preferences = NotificationPreferences.Create(Guid.NewGuid(), _userId);
        preferences.UpdateDnd(true, new TimeSpan(22, 0, 0), new TimeSpan(8, 0, 0));
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);

        var request = new UpdateNotificationPreferencesRequest
        {
            DndEnabled = false
        };

        var result = await _sut.UpdateAsync(_userId, request);

        result.DndEnabled.Should().BeFalse();
        result.DndStart.Should().BeNull();
        result.DndEnd.Should().BeNull();
    }

    #endregion

    #region UpdateAsync - Lazy Init

    [Fact]
    public async Task UpdateAsync_FirstAccess_ShouldLazyInit()
    {
        _repoMock.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var request = new UpdateNotificationPreferencesRequest
        {
            EventType = NotificationEventType.OrderStatus,
            Channel = NotificationChannel.Sms,
            Enabled = true
        };

        var result = await _sut.UpdateAsync(_userId, request);

        result.UserId.Should().Be(_userId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<NotificationPreferences>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
