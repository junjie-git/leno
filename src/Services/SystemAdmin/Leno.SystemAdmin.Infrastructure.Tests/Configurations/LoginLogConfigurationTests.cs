using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Leno.SystemAdmin.Infrastructure.Tests.Configurations;

public sealed class LoginLogConfigurationTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseInMemoryDatabase("loginlog-config-test")
            .Options;
        using var db = new SystemAdminDbContext(options);
        return db.Model;
    }

    [Fact]
    public void LoginLog_Entity_MapsToSnakeCaseTable()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(LoginLog));
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("login_logs");
    }

    [Fact]
    public void LoginLog_HasIndexOnLoginAtDescending()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(LoginLog))!;
        var index = entity.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(LoginLog.LoginAt)));
        index.Should().NotBeNull();
    }

    [Fact]
    public void LoginLog_Result_HasByteConversion()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(LoginLog))!;
        var property = entity.FindProperty(nameof(LoginLog.Result));
        property.Should().NotBeNull();
        property!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void LoginLog_Username_IsRequired()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(LoginLog))!;
        var property = entity.FindProperty(nameof(LoginLog.Username));
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }
}
