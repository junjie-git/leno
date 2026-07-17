using Leno.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Leno.Infrastructure.Tests.Configuration;

public class ConfigCenterExtensionsTests
{
    [Fact]
    public void ResolvePlaceholders_WithEnvVar_ShouldReplaceValue()
    {
        Environment.SetEnvironmentVariable("TEST_VAR_SK04", "resolved_value");
        try
        {
            var result = ConfigCenterExtensions.ResolvePlaceholders("${TEST_VAR_SK04}");

            result.Should().Be("resolved_value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_VAR_SK04", null);
        }
    }

    [Fact]
    public void ResolvePlaceholders_WithoutPlaceholder_ShouldReturnOriginal()
    {
        var result = ConfigCenterExtensions.ResolvePlaceholders("plain_value");

        result.Should().Be("plain_value");
    }

    [Fact]
    public void ResolvePlaceholders_Null_ShouldReturnEmpty()
    {
        var result = ConfigCenterExtensions.ResolvePlaceholders(null);

        result.Should().Be(string.Empty);
    }

    [Fact]
    public void ResolvePlaceholders_Empty_ShouldReturnEmpty()
    {
        var result = ConfigCenterExtensions.ResolvePlaceholders(string.Empty);

        result.Should().Be(string.Empty);
    }

    [Fact]
    public void ResolvePlaceholders_UnknownEnvVar_ShouldKeepPlaceholder()
    {
        var result = ConfigCenterExtensions.ResolvePlaceholders("${NONEXISTENT_VAR_XYZ}");

        result.Should().Be("${NONEXISTENT_VAR_XYZ}");
    }

    [Fact]
    public void GetResolvedValue_WithPlaceholder_ShouldResolve()
    {
        Environment.SetEnvironmentVariable("TEST_CONFIG_KEY", "secret_value");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TestKey"] = "${TEST_CONFIG_KEY}"
                })
                .Build();

            var result = config.GetResolvedValue("TestKey");

            result.Should().Be("secret_value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_CONFIG_KEY", null);
        }
    }

    [Fact]
    public void ResolveConfiguration_ShouldResolveAllPlaceholders()
    {
        Environment.SetEnvironmentVariable("DB_HOST", "prod-server");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionString"] = "Server=${DB_HOST};",
                    ["ApiKey"] = "plain_key"
                })
                .Build();

            var resolved = ConfigCenterExtensions.ResolveConfiguration(config);

            resolved["ConnectionString"].Should().Be("Server=prod-server;");
            resolved["ApiKey"].Should().Be("plain_key");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_HOST", null);
        }
    }

    [Fact]
    public void AddLenoConsulConfig_ValidBuilder_ShouldReturnSameBuilder()
    {
        var builder = Host.CreateApplicationBuilder([]);

        var result = builder.AddLenoConsulConfig();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddLenoConsulConfig_NullBuilder_ShouldThrow()
    {
        IHostApplicationBuilder builder = null!;

        var act = () => builder.AddLenoConsulConfig();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoConsulConfig_NullPrefix_ShouldThrow()
    {
        var builder = Host.CreateApplicationBuilder([]);

        var act = () => builder.AddLenoConsulConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoConsulConfig_WithCustomPrefix_ShouldConfigure()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Consul:Url"] = "http://consul:8500",
            ["Consul:Token"] = "test-token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var builder = Host.CreateApplicationBuilder([]);
        builder.Configuration.AddConfiguration(config);

        var result = builder.AddLenoConsulConfig("myapp/config");

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void ValidateSensitiveConfig_AllMissing_ShouldReturnFalse()
    {
        var config = new ConfigurationBuilder().Build();

        var result = config.ValidateSensitiveConfig();

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateSensitiveConfig_AllPresent_ShouldReturnTrue()
    {
        var configValues = new Dictionary<string, string?>();
        foreach (var key in ConfigCenterExtensions.SensitiveConfigKeys)
        {
            configValues[key] = "test-value";
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var result = config.ValidateSensitiveConfig();

        result.Should().BeTrue();
    }

    [Fact]
    public void GetMissingSensitiveConfigKeys_SomeMissing_ShouldReturnMissingKeys()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Payment:Alipay:AppId"] = "test",
            ["Jwt:SecretKey"] = "test"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var missing = config.GetMissingSensitiveConfigKeys();

        missing.Should().NotBeEmpty();
        missing.Should().NotContain("Payment:Alipay:AppId");
        missing.Should().NotContain("Jwt:SecretKey");
    }

    [Fact]
    public void SensitiveConfigKeys_ShouldContainExpectedKeys()
    {
        var keys = ConfigCenterExtensions.SensitiveConfigKeys;

        keys.Should().Contain("Payment:Alipay:AppId");
        keys.Should().Contain("Payment:WeChatPay:ApiKey");
        keys.Should().Contain("SMS:ApiKey");
        keys.Should().Contain("OAuth2:WeChat:AppSecret");
        keys.Should().Contain("Jwt:SecretKey");
    }

    [Fact]
    public void ValidateSensitiveConfig_MissingJwtSecretKey_ShouldReturnFalse()
    {
        // Arrange: 缺失 Jwt:SecretKey
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Payment:Alipay:AppId", "test" },
                { "Payment:Alipay:PrivateKey", "test" },
                { "Payment:Alipay:PublicKey", "test" }
            })
            .Build();

        // Act
        var isValid = config.ValidateSensitiveConfig();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateSensitiveConfig_AllKeysPresent_ShouldReturnTrue()
    {
        // Arrange: 所有敏感配置齐全
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test-secret-key-32-bytes-long!!" },
                { "Payment:Alipay:AppId", "test" },
                { "Payment:Alipay:PrivateKey", "test" },
                { "Payment:Alipay:PublicKey", "test" },
                { "Payment:WeChatPay:AppId", "test" },
                { "Payment:WeChatPay:MchId", "test" },
                { "Payment:WeChatPay:ApiKey", "test" },
                { "SMS:ApiKey", "test" },
                { "SMS:ApiSecret", "test" },
                { "OAuth2:WeChat:AppId", "test" },
                { "OAuth2:WeChat:AppSecret", "test" },
                { "OAuth2:Apple:ClientId", "test" },
                { "OAuth2:Apple:ClientSecret", "test" }
            })
            .Build();

        // Act
        var isValid = config.ValidateSensitiveConfig();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void GetMissingSensitiveConfigKeys_PartialConfig_ShouldReturnMissingKeys()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test" }
            })
            .Build();

        // Act
        var missing = config.GetMissingSensitiveConfigKeys();

        // Assert
        missing.Should().NotBeEmpty();
        missing.Should().Contain("Payment:Alipay:AppId");
    }
}