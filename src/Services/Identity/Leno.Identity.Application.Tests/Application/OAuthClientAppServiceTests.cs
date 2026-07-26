using Leno.Identity.Application.Abstractions;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Tests.Application;

/// <summary>
/// OAuthClientAppService 单元测试（Task A2 补齐）。
/// 覆盖 OAuth2 客户端的查询、新建、更新、启停及 ClientSecret 加密与掩码等场景。
/// </summary>
public class OAuthClientAppServiceTests
{
    private readonly Mock<IOAuthClientRepository> _oauthClientRepoMock = new();
    private readonly Mock<IClientSecretEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<OAuthClientAppService>> _loggerMock = new();
    private readonly OAuthClientAppService _sut;

    public OAuthClientAppServiceTests()
    {
        _encryptionServiceMock
            .Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns((string plain) => $"enc:{plain}");
        _encryptionServiceMock
            .Setup(e => e.Decrypt(It.IsAny<string>()))
            .Returns((string cipher) => cipher.StartsWith("enc:") ? cipher[4..] : cipher);

        _sut = new OAuthClientAppService(
            _oauthClientRepoMock.Object,
            _encryptionServiceMock.Object,
            _uowMock.Object,
            _loggerMock.Object);
    }

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_Should_Return_Masked_Dtos()
    {
        // Arrange：构造两个 OAuthClient，ClientSecret 已是密文（长度 ≥ 8）
        var client1 = CreateOAuthClient(provider: "google", secret: "enc-secret-google-12345");
        var client2 = CreateOAuthClient(provider: "wechat", secret: "enc-secret-wechat-67890");
        _oauthClientRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthClient> { client1, client2 });

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.First().Provider.Should().Be("google");
        result.First().ClientSecret.Should().StartWith("en")
            .And.Contain("****")
            .And.EndWith("45");
        result.First().ClientSecret.Should().NotBe(client1.ClientSecret, "密文不应原样返回");
        _oauthClientRepoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_With_Short_Secret_Should_Return_Full_Mask()
    {
        // Arrange：构造一个 ClientSecret 长度 < 8 的客户端
        var client = CreateOAuthClient(provider: "short", secret: "abc");
        _oauthClientRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthClient> { client });

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().ClientSecret.Should().Be("****");
    }

    [Fact]
    public async Task GetAllAsync_With_Empty_List_Should_Return_Empty()
    {
        _oauthClientRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OAuthClient>());

        var result = await _sut.GetAllAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_With_Valid_Request_Should_Encrypt_Secret_And_Default_Disabled()
    {
        // Arrange
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        var request = new OAuthClientDto
        {
            Provider = "google",
            ProviderType = "Oidc",
            ClientId = "google-client-id",
            ClientSecret = "super-secret-plain",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration",
            Scopes = new[] { "openid", "email" }
        };

        // Act
        await _sut.CreateAsync(request);

        // Assert
        _encryptionServiceMock.Verify(e => e.Encrypt("super-secret-plain"), Times.Once);
        _oauthClientRepoMock.Verify(
            r => r.AddAsync(It.Is<OAuthClient>(c =>
                c.Provider == "google" &&
                c.ClientSecret == "enc:super-secret-plain" &&
                c.Enabled == false), It.IsAny<CancellationToken>()),
            Times.Once,
            "新建默认 Enabled=false，需显式调用 EnableAsync 启用");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_With_Duplicate_Provider_Should_Throw_DomainException()
    {
        var existing = CreateOAuthClient(provider: "google", secret: "enc-existing");
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new OAuthClientDto
        {
            Provider = "google",
            ProviderType = "Oidc",
            ClientId = "new-client-id",
            ClientSecret = "new-secret",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration"
        };

        var act = async () => await _sut.CreateAsync(request);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("已存在");
        _oauthClientRepoMock.Verify(r => r.AddAsync(It.IsAny<OAuthClient>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_With_Empty_Provider_Should_Throw_DomainException()
    {
        var request = new OAuthClientDto
        {
            Provider = "",
            ProviderType = "Oidc",
            ClientId = "client-id",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration"
        };

        var act = async () => await _sut.CreateAsync(request);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("OAuth2 提供方不可为空");
    }

    [Fact]
    public async Task CreateAsync_With_Empty_ClientSecret_Should_Throw_DomainException()
    {
        var request = new OAuthClientDto
        {
            Provider = "github",
            ProviderType = "Oidc",
            ClientId = "client-id",
            ClientSecret = "",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration"
        };

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*ClientSecret 不可为空*");
    }

    [Fact]
    public async Task CreateAsync_With_Null_Request_Should_Throw_ArgumentNullException()
    {
        var act = async () => await _sut.CreateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_With_Missing_DiscoveryUrl_For_Oidc_Should_Throw_DomainException()
    {
        // OIDC 协议类型必须有 DiscoveryUrl，由 OAuthClient 聚合根校验
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        var request = new OAuthClientDto
        {
            Provider = "oidc-provider",
            ProviderType = "Oidc",
            ClientId = "client-id",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = null
        };

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*DiscoveryUrl*");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_With_Valid_Request_Should_Encrypt_Secret_And_Update()
    {
        var existing = CreateOAuthClient(provider: "google", secret: "enc-old-secret");
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new OAuthClientDto
        {
            Provider = "google",
            ProviderType = "Oidc",
            ClientId = "new-client-id",
            ClientSecret = "new-plain-secret",
            RedirectUri = "https://localhost/new-callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration"
        };

        await _sut.UpdateAsync("google", request);

        _encryptionServiceMock.Verify(e => e.Encrypt("new-plain-secret"), Times.Once);
        existing.ClientId.Should().Be("new-client-id");
        existing.ClientSecret.Should().Be("enc:new-plain-secret");
        existing.RedirectUri.Should().Be("https://localhost/new-callback");
        _oauthClientRepoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_With_Missing_Client_Should_Throw_DomainException()
    {
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        var request = new OAuthClientDto
        {
            Provider = "ghost",
            ProviderType = "Oidc",
            ClientId = "client-id",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration"
        };

        var act = async () => await _sut.UpdateAsync("ghost", request);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("不存在");
    }

    [Fact]
    public async Task UpdateAsync_With_Empty_Provider_Should_Throw_DomainException()
    {
        var request = new OAuthClientDto
        {
            Provider = "google",
            ProviderType = "Oidc",
            ClientId = "client-id",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback",
            DiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration"
        };

        var act = async () => await _sut.UpdateAsync("", request);

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*OAuth2 提供方不可为空*");
    }

    [Fact]
    public async Task UpdateAsync_With_Null_Request_Should_Throw_ArgumentNullException()
    {
        var act = async () => await _sut.UpdateAsync("google", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region EnableAsync / DisableAsync

    [Fact]
    public async Task EnableAsync_With_Existing_Client_Should_Enable_And_Save()
    {
        var client = CreateOAuthClient(provider: "google", secret: "enc-secret", enabled: false);
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        await _sut.EnableAsync("google");

        client.Enabled.Should().BeTrue();
        _oauthClientRepoMock.Verify(r => r.UpdateAsync(client, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_With_Missing_Client_Should_Throw_DomainException()
    {
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        var act = async () => await _sut.EnableAsync("ghost");

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*不存在*");
    }

    [Fact]
    public async Task EnableAsync_With_Empty_Provider_Should_Throw_DomainException()
    {
        var act = async () => await _sut.EnableAsync("");

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*OAuth2 提供方不可为空*");
    }

    [Fact]
    public async Task DisableAsync_With_Existing_Client_Should_Disable_And_Save()
    {
        var client = CreateOAuthClient(provider: "google", secret: "enc-secret", enabled: true);
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync("google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        await _sut.DisableAsync("google");

        client.Enabled.Should().BeFalse();
        _oauthClientRepoMock.Verify(r => r.UpdateAsync(client, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_With_Missing_Client_Should_Throw_DomainException()
    {
        _oauthClientRepoMock
            .Setup(r => r.GetByProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        var act = async () => await _sut.DisableAsync("ghost");

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*不存在*");
    }

    [Fact]
    public async Task DisableAsync_With_Empty_Provider_Should_Throw_DomainException()
    {
        var act = async () => await _sut.DisableAsync("");

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*OAuth2 提供方不可为空*");
    }

    #endregion

    /// <summary>创建测试 OAuthClient 聚合根。</summary>
    private static OAuthClient CreateOAuthClient(
        string provider = "google",
        string secret = "enc-secret",
        bool enabled = false)
    {
        return OAuthClient.Create(
            id: Guid.NewGuid(),
            provider: provider,
            providerType: "Oidc",
            clientId: "test-client-id",
            encryptedClientSecret: secret,
            redirectUri: "https://localhost/callback",
            scopes: new[] { "openid", "email" },
            discoveryUrl: "https://accounts.google.com/.well-known/openid-configuration",
            claimMappings: null,
            enabled: enabled);
    }
}
