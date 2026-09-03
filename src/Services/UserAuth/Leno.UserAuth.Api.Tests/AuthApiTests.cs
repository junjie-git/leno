using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.UserAuth.Api.Tests;

public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            // Development 环境：敏感配置缺失仅告警不阻断（Program.cs 在非 Development 抛异常）
            builder.UseSetting("Environment", "Development");
            // 提供 OAuth2:AesKey（32 字节全零 Base64，仅测试用，非生产密钥），
            // 避免 AddUserAuthInfrastructure 的 fail-fast 校验抛异常
            builder.UseSetting("OAuth2:AesKey", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
            // 提供 32+ 字节的 Jwt:SecretKey（仅测试用），避免 JWT 校验因密钥长度不足返回 500
            builder.UseSetting("Jwt:SecretKey", "leno-user-auth-testing-secret-key-0123456789abcdef");

            builder.ConfigureServices(services =>
            {
                // 测试环境无 Redis：替换分布式锁使 MigrateWithLockAsync 跳过迁移
                TestWebHostHelper.ReplaceDistributedLockWithNullProvider(services);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Health_Live_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
