using Microsoft.Win32;
using System.Text.RegularExpressions;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// 扫描已安装的 Steam 游戏。
/// 说明：用简单正则解析 Valve 的 VDF 格式文件（而非完整实现 VDF 解析器），
/// 图标统一使用 Steam 客户端图标 —— Steam 没有稳定路径的每游戏独立图标缓存，
/// 逐个游戏提取图标会明显拖慢启动速度，这里做了取舍。
/// </summary>
public static class SteamGamesScanner
{
    public static async Task<List<DesktopItem>> ScanAsync()
    {
        var results = new List<DesktopItem>();

        var steamPath = GetSteamInstallPath();
        if (steamPath is null || !Directory.Exists(steamPath))
            return results;

        var steamExe = Path.Combine(steamPath, "steam.exe");
        var steamIcon = File.Exists(steamExe)
            ? await IconExtractor.GetIconAsync(steamExe, false)
            : null;

        var seenAppIds = new HashSet<string>();

        foreach (var libraryPath in GetLibraryFolders(steamPath))
        {
            var steamAppsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsDir)) continue;

            foreach (var manifest in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                string content;
                try { content = File.ReadAllText(manifest); }
                catch { continue; }

                var appId = Regex.Match(content, "\"appid\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                var name = Regex.Match(content, "\"name\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name)) continue;
                if (!seenAppIds.Add(appId)) continue; // 同一个游戏已经加过了（多个库路径重复指向同一文件夹等情况）

                results.Add(new DesktopItem
                {
                    FullPath = $"steam:{appId}",
                    DisplayName = name,
                    IsFolder = false,
                    IsInstalledApp = true,
                    UseSquareIcon = true,
                    LaunchTarget = $"steam://rungameid/{appId}",
                    Icon = steamIcon
                });
            }
        }

        return results;
    }

    private static string? GetSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return (key?.GetValue("SteamPath") as string) ?? (key?.GetValue("InstallPath") as string);
    }

    private static List<string> GetLibraryFolders(string steamPath)
    {
        var normalizedSteamPath = steamPath.TrimEnd('\\');
        var libraries = new List<string> { normalizedSteamPath };

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return libraries;

        string content;
        try { content = File.ReadAllText(vdfPath); }
        catch { return libraries; }

        foreach (Match m in Regex.Matches(content, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var path = m.Groups[1].Value.Replace(@"\\", @"\").TrimEnd('\\');
            if (Directory.Exists(path) && !libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                libraries.Add(path);
        }

        return libraries;
    }
}
