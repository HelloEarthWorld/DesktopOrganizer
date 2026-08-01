using System.Text.Json;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// 把用户的分组结构（哪些文件属于哪个分组）保存在
/// %LOCALAPPDATA%\DesktopOrganizer\groups.json
/// 不写入用户桌面，不移动任何真实文件。
/// </summary>
public class GroupStorageService
{
    private readonly string _filePath;

    public GroupStorageService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopOrganizer");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "groups.json");
    }

    public List<ItemGroup> Load()
    {
        if (!File.Exists(_filePath))
            return new List<ItemGroup>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var groups = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListItemGroup);
            return groups ?? new List<ItemGroup>();
        }
        catch
        {
            // 文件损坏时不崩溃，退回空列表，MainWindow 会自动重建默认分组
            return new List<ItemGroup>();
        }
    }

    public void Save(IEnumerable<ItemGroup> groups)
    {
        var groupList = groups.ToList();
        // 保存前把运行时 Items 同步回 ItemPaths
        foreach (var g in groupList)
        {
            g.ItemPaths = g.Items.Select(i => i.FullPath).ToList();
        }

        var json = JsonSerializer.Serialize(groupList, AppJsonContext.Default.ListItemGroup);
        File.WriteAllText(_filePath, json);
    }
}
