namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>菜单节点类型。</summary>
public enum MenuType
{
    /// <summary>目录节点：可包含子菜单，Path 可空或目录前缀。</summary>
    Directory = 1,

    /// <summary>菜单节点：路由项，Component 必填。</summary>
    Menu = 2,

    /// <summary>按钮节点：权限点，Path 必须为 null。</summary>
    Button = 3
}
