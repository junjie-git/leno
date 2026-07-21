using Leno.Notification.Infrastructure.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Leno.Notification.Infrastructure.Tests.Channels;

public class ChannelOptionsBindingTests
{
    private static IServiceCollection BuildServicesWithConfig(string json)
    {
        var config = new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.Configure<EmailChannelOptions>(config.GetSection("Notification:Email"));
        services.Configure<SmsChannelOptions>(config.GetSection("Notification:Sms"));
        return services;
    }

    [Fact]
    public void EmailChannelOptions_ShouldBindHostFromAppSettings()
    {
        // Arrange — 使用修复后的 appsettings.json 字段名（Host/From/UseSsl）
        var json = """{"Notification":{"Email":{"Host":"smtp.example.com","Port":587,"Username":"user","Password":"pass","From":"noreply@example.com","UseSsl":true}}}""";
        var services = BuildServicesWithConfig(json);
        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<EmailChannelOptions>>().Value;

        // Assert — 修复前 Host 为空（appsettings 用 SmtpHost），修复后正确绑定
        Assert.Equal("smtp.example.com", options.Host);
        Assert.Equal(587, options.Port);
        Assert.Equal("noreply@example.com", options.From);
        Assert.True(options.UseSsl);
    }

    [Fact]
    public void SmsChannelOptions_ShouldBindAccessKeyIdFromAppSettings()
    {
        // Arrange — 使用修复后的 appsettings.json 字段名（AccessKeyId/AccessKeySecret）
        var json = """{"Notification":{"Sms":{"Provider":"Aliyun","AccessKeyId":"AKID123","AccessKeySecret":"SK456","SignName":"Leno"}}}""";
        var services = BuildServicesWithConfig(json);
        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<SmsChannelOptions>>().Value;

        // Assert — 修复前 AccessKeyId 为空（appsettings 用 AccessKey），修复后正确绑定
        Assert.Equal("AKID123", options.AccessKeyId);
        Assert.Equal("SK456", options.AccessKeySecret);
        Assert.Equal("Leno", options.SignName);
    }
}
