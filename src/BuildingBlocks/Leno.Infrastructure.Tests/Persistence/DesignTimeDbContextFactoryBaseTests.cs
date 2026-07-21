using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Persistence;

/// <summary>
/// 设计期 DbContext 工厂基类的连接字符串解析测试。
/// 验证从环境变量读取连接字符串，且不包含硬编码 SA 密码。
/// </summary>
public class DesignTimeDbContextFactoryBaseTests
{
    /// <summary>
    /// 测试专用 DbContext，仅用于满足泛型约束 where TContext : DbContext。
    /// </summary>
    private sealed class TestDbContext : DbContext
    {
    }

    [Fact]
    public void ResolveConnectionString_ShouldReadFromEnvironmentVariable()
    {
        // Arrange — 设置环境变量
        var expectedConnStr = "Server=test,1433;Database=LenoTest;User Id=sa;Password=FromEnv;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", expectedConnStr);

        try
        {
            // Act
            var resolved = DesignTimeDbContextFactoryBase<TestDbContext>.ResolveConnectionString("LenoTest");

            // Assert — 应从环境变量读取，而非硬编码
            resolved.Should().Be(expectedConnStr, "应从 LENO_DESIGNTIME_CONNECTION_STRING 环境变量读取连接字符串");
            resolved.Should().NotContain("Leno@SqlServer2019",
                "不应包含硬编码的 SA 密码");
        }
        finally
        {
            // 清理环境变量
            Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", null);
        }
    }

    [Fact]
    public void ResolveConnectionString_NotSet_ShouldThrowWithClearMessage()
    {
        // Arrange — 清除环境变量
        Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", null);

        // Act & Assert — 未配置时应抛异常并给出明确提示
        var act = () => DesignTimeDbContextFactoryBase<TestDbContext>.ResolveConnectionString("LenoTest");
        var ex = act.Should().Throw<InvalidOperationException>().Subject;
        ex.Message.Should().Contain("LENO_DESIGNTIME_CONNECTION_STRING",
            "异常消息应提示需要设置环境变量");
        ex.Message.Should().NotContain("Leno@SqlServer2019",
            "异常消息不应暴露旧密码");
    }

    [Fact]
    public void ResolveConnectionString_ShouldNeverContainLegacyPassword()
    {
        // Arrange — 各种环境变量场景
        var testValues = new[]
        {
            "Server=localhost,1433;Database=Test;User Id=sa;Password=AnyPassword;TrustServerCertificate=True",
            null,
            ""
        };

        foreach (var value in testValues)
        {
            Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", value);
            try
            {
                if (string.IsNullOrEmpty(value))
                {
                    var act = () => DesignTimeDbContextFactoryBase<TestDbContext>.ResolveConnectionString("Test");
                    act.Should().Throw<InvalidOperationException>("空值应抛异常");
                }
                else
                {
                    var resolved = DesignTimeDbContextFactoryBase<TestDbContext>.ResolveConnectionString("Test");
                    resolved.Should().NotContain("Leno@SqlServer2019",
                        "解析结果绝不应包含旧硬编码密码");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", null);
            }
        }
    }
}
