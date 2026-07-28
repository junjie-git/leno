using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Leno.SystemAdmin.Infrastructure.Tests.Configurations;

public sealed class MenuConfigurationTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseInMemoryDatabase("menu-config-test")
            .Options;
        using var db = new SystemAdminDbContext(options);
        return db.Model;
    }

    [Fact]
    public void Menu_Entity_MapsToSnakeCaseTable()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(Menu));
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("menus");
    }

    [Fact]
    public void Menu_HasIndexOnParentId()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(Menu))!;
        var index = entity.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Menu.ParentId)));
        index.Should().NotBeNull();
    }

    [Fact]
    public void Menu_Roles_HasJsonConversion()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(Menu))!;
        var property = entity.FindProperty(nameof(Menu.Roles));
        property.Should().NotBeNull();
        property!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void Menu_Type_HasByteConversion()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(typeof(Menu))!;
        var property = entity.FindProperty(nameof(Menu.Type));
        property.Should().NotBeNull();
        property!.GetValueConverter().Should().NotBeNull();
    }
}
