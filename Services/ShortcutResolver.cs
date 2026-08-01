using System.Runtime.InteropServices;

namespace DesktopOrganizer.Services;

/// <summary>
/// 用 WScript.Shell（Windows 自带的 COM 组件）后期绑定解析 .lnk 快捷方式的
/// 真实目标路径，不需要额外引用 COM Interop 程序集。
/// </summary>
public static class ShortcutResolver
{
    public static (string TargetPath, string Arguments)? Resolve(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath ?? string.Empty;
            string args = shortcut.Arguments ?? string.Empty;

            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);

            return string.IsNullOrEmpty(target) ? null : (target, args);
        }
        catch
        {
            // 解析失败（损坏的快捷方式等）时不崩溃，调用方按普通文件处理
            return null;
        }
    }
}
