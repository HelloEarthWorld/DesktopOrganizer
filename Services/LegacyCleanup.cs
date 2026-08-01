using Microsoft.Win32;

namespace DesktopOrganizer.Services;

/// <summary>
/// 一次性清理：删除旧版本写入的开机自启动注册表项。
/// 现在的版本不再支持开机自启动/后台常驻，启动时顺手把之前留下的痕迹清掉。
/// </summary>
public static class LegacyCleanup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DesktopOrganizerApps";

    public static void RemoveOldAutoStartEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(AppName) != null)
                key.DeleteValue(AppName);
        }
        catch
        {
            // 清理失败不影响正常使用，忽略
        }
    }
}
