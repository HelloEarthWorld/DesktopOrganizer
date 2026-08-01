using System.Text.RegularExpressions;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// 汇总"应用"来源：桌面上的所有条目（exe/com 本体 + 指向 exe/com 的快捷方式归为应用，
/// 其余归为文件）+ Windows 已安装程序列表 + Steam 游戏。
/// 去重规则：桌面快捷方式指向的程序如果已在已安装程序/Steam 列表里出现过，
/// 优先显示注册表/库里的那一份（图标、卸载信息更全），桌面那份不重复显示。
/// </summary>
public static class AppAggregatorService
{
    public record ScanResult(List<DesktopItem> Apps, List<DesktopItem> Files);

    public static async Task<ScanResult> ScanAsync()
    {
        var desktopItems = await SafeScanAsync(DesktopScanner.ScanAsync, "桌面文件");
        var installedTask = SafeScanAsync(InstalledProgramsScanner.ScanAsync, "已安装程序");
        var steamTask = SafeScanAsync(SteamGamesScanner.ScanAsync, "Steam游戏");
        await Task.WhenAll(installedTask, steamTask);

        var installedApps = installedTask.Result;
        var steamApps = steamTask.Result;

        var knownTargets = new HashSet<string>(
            installedApps.Where(p => !string.IsNullOrEmpty(p.LaunchTarget))
                         .Select(p => NormalizePath(p.LaunchTarget)),
            StringComparer.OrdinalIgnoreCase);

        var apps = new List<DesktopItem>();
        var files = new List<DesktopItem>();
        var appIdsCoveredByShortcut = new HashSet<string>(); // Steam快捷方式已经代表过的appid，库里那条要被排除

        foreach (var item in desktopItems)
        {
            var ext = Path.GetExtension(item.FullPath).ToLowerInvariant();

            if (ext == ".lnk")
            {
                var resolved = ShortcutResolver.Resolve(item.FullPath);
                if (resolved is { } r && !string.IsNullOrEmpty(r.TargetPath))
                {
                    // Steam 快捷方式有两种写法：目标 steam.exe + "-applaunch 123" 参数，
                    // 或直接把 "steam://rungameid/123" 协议当作目标，两种都要认出来
                    string? steamAppId = null;
                    if (r.TargetPath.EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase)
                        && r.Arguments.Contains("applaunch", StringComparison.OrdinalIgnoreCase))
                    {
                        steamAppId = Regex.Match(r.Arguments, @"applaunch\s*(\d+)").Groups[1].Value;
                    }
                    else if (r.TargetPath.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase))
                    {
                        steamAppId = r.TargetPath["steam://rungameid/".Length..];
                    }

                    if (!string.IsNullOrEmpty(steamAppId) && Regex.IsMatch(steamAppId, @"^\d+$"))
                    {
                        // 优先显示桌面快捷方式（不包括 Steam 客户端本体），Steam 库里对应那条稍后会被排除。
                        // 不要求库里必须扫得到：即使 Steam 库解析失败，桌面快捷方式本身也是有效游戏入口
                        appIdsCoveredByShortcut.Add(steamAppId);
                        item.LaunchTarget = $"steam://rungameid/{steamAppId}";
                        item.ShortcutTargetPath = r.TargetPath;
                        item.UseSquareIcon = true;
                        apps.Add(item);
                        continue;
                    }

                    var targetExt = Path.GetExtension(r.TargetPath).ToLowerInvariant();
                    if (targetExt is ".exe" or ".com")
                    {
                        item.LaunchTarget = r.TargetPath;
                        item.ShortcutTargetPath = r.TargetPath;
                        item.UseSquareIcon = true;
                        if (knownTargets.Contains(NormalizePath(r.TargetPath)))
                            continue; // 已安装程序列表里已经有同一个目标了，跳过重复

                        apps.Add(item);
                        continue;
                    }

                    item.ShortcutTargetPath = r.TargetPath;
                }

                // 解析失败，或指向的是文件夹之类的非可执行目标，当普通文件处理
                item.LaunchTarget = item.FullPath;
                files.Add(item);
            }
            else if (ext is ".exe" or ".com")
            {
                item.LaunchTarget = item.FullPath;
                item.UseSquareIcon = true;
                if (!knownTargets.Contains(NormalizePath(item.FullPath)))
                    apps.Add(item);
            }
            else
            {
                item.LaunchTarget = item.FullPath;
                files.Add(item);
            }
        }

        apps.AddRange(installedApps);
        // 已经有桌面快捷方式代表的 Steam 游戏，库里这条不再重复添加
        apps.AddRange(steamApps.Where(s => !appIdsCoveredByShortcut.Contains(s.FullPath.Replace("steam:", ""))));

        return new ScanResult(apps, files);
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\'); }
        catch { return path; }
    }

    /// <summary>某一个扫描来源（已安装程序/Steam等）失败时返回空列表，不影响其它来源正常显示</summary>
    private static async Task<List<DesktopItem>> SafeScanAsync(Func<Task<List<DesktopItem>>> scan, string sourceName)
    {
        try
        {
            return await scan();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppAggregatorService] {sourceName} 扫描失败: {ex}");
            return new List<DesktopItem>();
        }
    }
}
