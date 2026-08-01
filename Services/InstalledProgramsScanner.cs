using Microsoft.Win32;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// 扫描 Windows 已安装程序列表（注册表 Uninstall 项），
/// 用于识别"已安装的应用"，并给桌面快捷方式去重提供依据。
/// </summary>
public static class InstalledProgramsScanner
{
    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallKeyPathWow6432 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    // 这些是常见的"技术性组件"，不是用户会主动去点开用的东西，过滤掉减少列表噪音
    private static readonly string[] NoisyNameKeywords =
    {
        "Visual Studio Installer",
        "Visual C++ ",
        "Visual Studio Tools for",
        ".NET Runtime",
        ".NET Desktop Runtime",
        ".NET Host",
        ".NET SDK",
        "Windows Software Development Kit",
        "Windows App SDK",
        "Windows Driver Kit",
        "Microsoft Update Health",
        "Update for Microsoft",
        "Security Update for",
        "Hotfix for",
        "Redistributable",
    };

    public static async Task<List<DesktopItem>> ScanAsync()
    {
        var basics = new List<(string SubKeyName, string DisplayName, string ExePath, string? UninstallCommand)>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var keyPath in new[] { UninstallKeyPath, UninstallKeyPathWow6432 })
            {
                using var uninstallKey = root.OpenSubKey(keyPath);
                if (uninstallKey is null) continue;

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey is null) continue;

                        var displayName = appKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        // 过滤系统组件/补丁更新/开发工具技术组件，避免列表被垃圾数据淹没
                        if ((appKey.GetValue("SystemComponent") as int?) == 1) continue;
                        if (appKey.GetValue("ParentKeyName") != null) continue;
                        if (NoisyNameKeywords.Any(k => displayName.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
                        if (!seenNames.Add(displayName)) continue; // 同名去重

                        var exePath = ExtractExePath(appKey);
                        if (string.IsNullOrEmpty(exePath)) continue; // 找不到可执行文件就不显示，避免点了没反应

                        var uninstallCommand = appKey.GetValue("UninstallString") as string;
                        basics.Add((subKeyName, displayName, exePath, uninstallCommand));
                    }
                    catch
                    {
                        // 单个注册表项读取失败（权限/损坏数据等）不影响其它程序，跳过继续
                    }
                }
            }
        }

        // 图标提取并发进行，装的软件多的话明显更快
        var tasks = basics.Select(async b => new DesktopItem
        {
            FullPath = $"installed:{b.SubKeyName}",
            DisplayName = b.DisplayName,
            IsFolder = false,
            IsInstalledApp = true,
            UseSquareIcon = true,
            LaunchTarget = b.ExePath,
            UninstallCommand = b.UninstallCommand,
            Icon = await IconExtractor.GetIconAsync(b.ExePath, false)
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private static string? ExtractExePath(RegistryKey appKey)
    {
        // DisplayIcon 通常形如 "C:\Path\App.exe,0"，去掉逗号后面的图标索引
        var displayIcon = appKey.GetValue("DisplayIcon") as string;
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var path = displayIcon.Split(',')[0].Trim('"');
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                return path;
        }

        var installLocation = appKey.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            return Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
        }

        return null;
    }
}
