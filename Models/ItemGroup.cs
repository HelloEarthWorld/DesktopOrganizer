using System.Collections.ObjectModel;

namespace DesktopOrganizer.Models;

/// <summary>
/// 用户自定义分组。持久化时只存 ItemPaths（路径列表），
/// 运行时再把真实存在的 DesktopItem 挂到 Items 集合供 UI 绑定。
/// </summary>
public class ItemGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "新分组";

    /// <summary>展开/折叠状态，记住用户上次的偏好</summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>系统固定分组（比如"系统"分组），不允许被用户删除</summary>
    public bool IsSystem { get; set; }

    /// <summary>持久化用：只存路径</summary>
    public List<string> ItemPaths { get; set; } = new();

    /// <summary>运行时用：绑定到 UI 的真实条目集合（不参与序列化）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ObservableCollection<DesktopItem> Items { get; set; } = new();
}
