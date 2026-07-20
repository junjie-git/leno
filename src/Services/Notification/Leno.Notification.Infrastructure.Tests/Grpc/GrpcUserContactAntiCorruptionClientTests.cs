using FluentAssertions;
using Grpc.Core;
using Leno.Infrastructure.AntiCorruption;
using Leno.Notification.Infrastructure.Services.Grpc;
using Leno.SharedContracts.Grpc.User.V1;
using Leno.Testing.Builders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Grpc;

public class GrpcUserContactAntiCorruptionClientTests
{
    private static IOptionsMonitor<AntiCorruptionOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<AntiCorruptionOptions>>();
        mock.SetupGet(o => o.CurrentValue).Returns(new AntiCorruptionOptions
        {
            UseGrpc = true,
            TargetInternalApiKeys = new Dictionary<string, string> { { "UserAuth", "test-key" } }
        });
        return mock.Object;
    }

    [Fact]
    public async Task GetContacts_Success_ReturnsMappedDto()
    {
        // UserInternalServiceClient 有 protected 无参构造函数，Moq 可直接 mock
        var clientMock = new Mock<UserInternalService.UserInternalServiceClient>();
        var userId = Guid.NewGuid();
        var response = new UserContacts
        {
            UserId = userId.ToString(),
            Email = "test@example.com",
            Phone = "13800000000"
        };

        clientMock.Setup(c => c.GetUserContactsAsync(
                It.IsAny<GetUserContactsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<UserContacts>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new GrpcUserContactAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcUserContactAntiCorruptionClient>.Instance);

        var result = await client.GetContactsAsync(userId);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
        result.PhoneNumber.Should().Be("13800000000");
    }

    [Fact]
    public async Task GetContacts_Unavailable_ThrowsAntiCorruptionException_WithRpcInner()
    {
        var clientMock = new Mock<UserInternalService.UserInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, "down"));

        clientMock.Setup(c => c.GetUserContactsAsync(
                It.IsAny<GetUserContactsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcUserContactAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcUserContactAntiCorruptionClient>.Instance);

        var act = async () => await client.GetContactsAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.InnerException.Should().BeSameAs(rpcEx);
        thrown.ErrorCode.Should().Be("USER_CONTACT_UNAVAILABLE");
    }

    [Fact]
    public async Task GetContacts_NotFound_ThrowsAntiCorruptionException_RemoteFailed()
    {
        var clientMock = new Mock<UserInternalService.UserInternalServiceClient>();
        var rpcEx = new RpcException(new Status(StatusCode.NotFound, "user missing"));

        clientMock.Setup(c => c.GetUserContactsAsync(
                It.IsAny<GetUserContactsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(rpcEx);

        var client = new GrpcUserContactAntiCorruptionClient(GrpcAntiCorruptionTestHelper.BuildServiceProviderWithGrpcRetry(), clientMock.Object, CreateOptionsMonitor(),
            NullLogger<GrpcUserContactAntiCorruptionClient>.Instance);

        var act = async () => await client.GetContactsAsync(Guid.NewGuid());

        var thrown = (await act.Should().ThrowAsync<AntiCorruptionException>()).Which;
        thrown.ErrorCode.Should().Be("USER_CONTACT_REMOTE_FAILED");
    }
}
