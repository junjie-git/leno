using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Tests;

public class OAuthClientTests
{
    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateOAuthClient()
    {
        var client = OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id-123",
            "encrypted-secret", "https://example.com/callback");

        client.Provider.Should().Be("google");
        client.ClientId.Should().Be("client-id-123");
        client.ClientSecret.Should().Be("encrypted-secret");
        client.RedirectUri.Should().Be("https://example.com/callback");
        client.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Create_ProviderNormalized_ShouldBeLowercase()
    {
        var client = OAuthClient.Create(
            Guid.NewGuid(), "GOOGLE", "client-id", "secret", "https://example.com/callback");

        client.Provider.Should().Be("google");
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => OAuthClient.Create(
            Guid.Empty, "google", "client-id", "secret", "https://example.com/callback");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyProvider_ShouldThrowException()
    {
        var act = () => OAuthClient.Create(
            Guid.NewGuid(), "", "client-id", "secret", "https://example.com/callback");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyClientId_ShouldThrowException()
    {
        var act = () => OAuthClient.Create(
            Guid.NewGuid(), "google", "", "secret", "https://example.com/callback");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptySecret_ShouldThrowException()
    {
        var act = () => OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "", "https://example.com/callback");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyRedirectUri_ShouldThrowException()
    {
        var act = () => OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "secret", "");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_InvalidRedirectUri_ShouldThrowException()
    {
        var act = () => OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "secret", "not-a-valid-uri");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_DisabledClient_ShouldHaveEnabledFalse()
    {
        var client = OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "secret", "https://example.com/callback", enabled: false);

        client.Enabled.Should().BeFalse();
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ValidParameters_ShouldUpdateFields()
    {
        var client = OAuthClient.Create(
            Guid.NewGuid(), "google", "old-client-id", "old-secret", "https://old.example.com/callback");

        client.Update("new-client-id", "new-secret", "https://new.example.com/callback");

        client.ClientId.Should().Be("new-client-id");
        client.ClientSecret.Should().Be("new-secret");
        client.RedirectUri.Should().Be("https://new.example.com/callback");
    }

    [Fact]
    public void Update_EmptyClientId_ShouldThrowException()
    {
        var client = CreateClient();

        var act = () => client.Update("", "secret", "https://example.com/callback");

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Update_EmptySecret_ShouldThrowException()
    {
        var client = CreateClient();

        var act = () => client.Update("client-id", "", "https://example.com/callback");

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region Enable / Disable

    [Fact]
    public void Enable_DisabledClient_ShouldSetEnabledTrue()
    {
        var client = OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "secret", "https://example.com/callback", enabled: false);

        client.Enable();

        client.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_AlreadyEnabled_ShouldStayEnabled()
    {
        var client = CreateClient();

        client.Enable();

        client.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_EnabledClient_ShouldSetEnabledFalse()
    {
        var client = CreateClient();

        client.Disable();

        client.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_AlreadyDisabled_ShouldStayDisabled()
    {
        var client = OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "secret", "https://example.com/callback", enabled: false);

        client.Disable();

        client.Enabled.Should().BeFalse();
    }

    #endregion

    private static OAuthClient CreateClient()
    {
        return OAuthClient.Create(
            Guid.NewGuid(), "google", "client-id", "secret", "https://example.com/callback");
    }
}