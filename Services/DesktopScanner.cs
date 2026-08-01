using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// 扫描用户真实桌面（含公共桌面）里的文件和文件夹。
/// 只读扫描，不做任何写入/移动操作。
/// </summary>
public static class DesktopScanner
{
    public static string DesktopPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string PublicDesktopPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    /// <summary>
    /// 枚举桌面条目：用户桌面 + 公共桌面。Windows 资源管理器看到的桌面就是两者的合并视图，
    /// 很多安装器只把快捷方式放到公共桌面，只扫一个会漏应用。
    /// 同名文件（如两个 App.lnk）时用户桌面优先、公共桌面跳过，和资源管理器一致。
    /// 忽略隐藏/系统文件（比如 desktop.ini），并发提取图标以加快首次加载速度。
    /// </summary>
    public static async Task<List<DesktopItem>> ScanAsync()
    {
        var items = new List<DesktopItem>();
        var entries = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var desktopPath in new[] { DesktopPath, PublicDesktopPath })
        {
            if (!Directory.Exists(desktopPath)) continue;

            foreach (var path in Directory.EnumerateFileSystemEntries(desktopPath))
            {
                try
                {
                    var name = Path.GetFileName(path);
                    if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    var attrs = File.GetAttributes(path);
                    if (attrs.HasFlag(FileAttributes.Hidden) || attrs.HasFlag(FileAttributes.System)) continue;
                    if (!seenNames.Add(name)) continue; // 用户桌面已有同名文件，公共桌面这份不重复
                    entries.Add(path);
                }
                catch
                {
                    // 权限不足/云同步占位文件等读取失败时，跳过这一项而不是让整个扫描崩掉
                }
            }
        }

        var tasks = entries.Select(async path =>
        {
            try
            {
                bool isFolder = Directory.Exists(path);
                var icon = await IconExtractor.GetIconAsync(path, isFolder);
                return new DesktopItem
                {
                    FullPath = path,
                    DisplayName = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n && !isFolder
                        ? n
                        : Path.GetFileName(path),
                    IsFolder = isFolder,
                    Icon = icon
                };
            }
            catch
            {
                // 单个图标提取失败不应影响其它文件的加载
                return new DesktopItem
                {
                    FullPath = path,
                    DisplayName = Path.GetFileName(path),
                    IsFolder = Directory.Exists(path),
                    Icon = null
                };
            }
        });

        var results = await Task.WhenAll(tasks);
        items.AddRange(results);
        return items;
    }
}
