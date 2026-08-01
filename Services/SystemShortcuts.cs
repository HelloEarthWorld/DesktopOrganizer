using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// 生成"此电脑 / 控制面板 / 回收站 / 运行 / 终端 / 设置"这些系统级快捷方式。
/// 图标优先从 SHELL32.dll 按索引提取（跟系统原生一致），提取失败时用
/// CLSID 特殊路径兜底，保证不会因为某个索引不对就整个图标空白。
/// </summary>
public static class SystemShortcuts
{
    private const string Shell32 = @"%SystemRoot%\System32\SHELL32.dll";
    private const string Shell32Mun = @"%SystemRoot%\SystemResources\shell32.dll.mun";
    private const string Imageres = @"%SystemRoot%\System32\imageres.dll";

    // Windows 内置的、多年不变的特殊文件夹 GUID：直接让 Shell 按 CLSID 取图标（跟资源管理器完全一致）
    private const string ThisPcClsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string ControlPanelClsid = "::{21EC2020-3AEA-1069-A2DD-08002B30309D}";
    private const string RecycleBinClsid = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    // 设置/终端的图标在各自的 AppX 包资源里，用 AUMID 走 shell:AppsFolder 取，跟开始菜单显示的一模一样
    private const string SettingsAppFolder = @"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel";
    private const string TerminalAppFolder = @"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";
    private const string SettingsAppExe = @"C:\Windows\ImmersiveControlPanel\SystemSettings.exe";
    // 打开 Windows Terminal 的常见安装路径（通过 WindowsApps 别名，装了就有这个文件）
    private const string TerminalExe = @"%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe";

    public static async Task<List<DesktopItem>> BuildAsync()
    {
        var shell32Path = Environment.ExpandEnvironmentVariables(Shell32);
        var shell32MunPath = Environment.ExpandEnvironmentVariables(Shell32Mun);

        // 特殊文件夹优先按 CLSID 解析 Shell 项取图标（跟资源管理器图标一致），
        // 索引是备份方案，万一某个 DLL 版本索引对不上也不至于空白
        var thisPcIcon = await GetIconWithFallback(ThisPcClsid, shell32Path, 15);
        var controlPanelIcon = await GetIconWithFallback(ControlPanelClsid, shell32Path, 20);
        var recycleBinIcon = await GetIconWithFallback(RecycleBinClsid, shell32Path, 32);
        // "运行"图标：优先从 shell32.dll.mun 直接按资源 ID 25 取（Win11 新样式），退回 System32 桩 DLL
        var runIcon = await IconExtractor.GetIconFromResourceAsync(shell32MunPath, 25)
            ?? await IconExtractor.GetIconFromDllAsync(shell32Path, 25);
        // "设置"图标：取设置应用包的真实图标（新版样式），失败再退回 imageres 索引 / SystemSettings.exe
        var settingsIcon = await IconExtractor.GetShellItemIconAsync(SettingsAppFolder)
            ?? await IconExtractor.GetIconFromDllAsync(Environment.ExpandEnvironmentVariables(Imageres), 16826)
            ?? await IconExtractor.GetIconAsync(SettingsAppExe, isFolder: false);

        var terminalPath = Environment.ExpandEnvironmentVariables(TerminalExe);
        // "终端"图标：取 Windows Terminal 应用包的真实图标，退回 wt.exe 别名（可能只有通用图标）
        var terminalIcon = await IconExtractor.GetShellItemIconAsync(TerminalAppFolder)
            ?? (File.Exists(terminalPath) ? await IconExtractor.GetIconAsync(terminalPath, isFolder: false) : null);

        var list = new List<DesktopItem>
        {
            new()
            {
                FullPath = "system:thispc",
                DisplayName = "此电脑",
                IsFolder = true,
                IsSystemShortcut = true,
                UseSquareIcon = true,
                LaunchTarget = "shell:MyComputerFolder",
                Icon = thisPcIcon
            },
            new()
            {
                FullPath = "system:controlpanel",
                DisplayName = "控制面板",
                IsFolder = true,
                IsSystemShortcut = true,
                UseSquareIcon = true,
                LaunchTarget = "shell:ControlPanelFolder",
                Icon = controlPanelIcon
            },
            new()
            {
                FullPath = "system:recyclebin",
                DisplayName = "回收站",
                IsFolder = true,
                IsSystemShortcut = true,
                UseSquareIcon = true,
                LaunchTarget = "shell:RecycleBinFolder",
                Icon = recycleBinIcon
            },
            new()
            {
                FullPath = "system:run",
                DisplayName = "运行",
                IsFolder = false,
                IsSystemShortcut = true,
                UseSquareIcon = true,
                // "运行"对话框没有独立可执行文件，这是打开它的标准 shell 方式
                LaunchTarget = "shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}",
                Icon = runIcon
            },
            new()
            {
                FullPath = "system:settings",
                DisplayName = "设置",
                IsFolder = false,
                IsSystemShortcut = true,
                UseSquareIcon = true,
                LaunchTarget = "ms-settings:",
                Icon = settingsIcon
            }
        };

        // 终端图标不在 SHELL32.dll 里，只有真装了 Windows Terminal 才显示这一项
        if (terminalIcon != null)
        {
            list.Add(new DesktopItem
            {
                FullPath = "system:terminal",
                DisplayName = "终端",
                IsFolder = false,
                IsSystemShortcut = true,
                UseSquareIcon = true,
                LaunchTarget = terminalPath,
                Icon = terminalIcon
            });
        }

        return list;
    }

    /// <summary>优先按 CLSID 走 Shell 命名空间解析取真实图标（跟资源管理器一致），失败再退回 DLL 索引</summary>
    private static async Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage?> GetIconWithFallback(
        string clsidPath, string dllPath, int dllIndex)
    {
        var icon = await IconExtractor.GetShellItemIconAsync(clsidPath);
        return icon ?? await IconExtractor.GetIconFromDllAsync(dllPath, dllIndex);
    }
}
