using Microsoft.UI.Xaml.Media.Imaging;

namespace DesktopOrganizer.Models;

/// <summary>
/// 代表用户真实桌面上的一个文件或文件夹。
/// 只读取图标和路径信息，不修改原始文件位置。
/// </summary>
public class DesktopItem
{
    /// <summary>完整路径，作为唯一标识（分组归属用这个做 Key）</summary>
    public string FullPath { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsFolder { get; set; }

    /// <summary>是否是"此电脑/控制面板/设置"这类系统内置快捷方式，而非真实桌面文件</summary>
    public bool IsSystemShortcut { get; set; }

    /// <summary>是否来自"已安装程序/Steam游戏"列表，而非桌面扫描</summary>
    public bool IsInstalledApp { get; set; }

    /// <summary>点击后实际启动的目标：真实文件用完整路径，系统/已安装程序用协议或解析出的exe路径</summary>
    public string LaunchTarget { get; set; } = string.Empty;

    /// <summary>true=按 macOS 风格的圆角方形裁切显示（应用/快捷方式）；false=按原图完整显示（普通文件）</summary>
    public bool UseSquareIcon { get; set; }

    /// <summary>仅快捷方式(.lnk)有值：真实指向的目标路径，右键"查看快捷方式位置"用</summary>
    public string? ShortcutTargetPath { get; set; }

    /// <summary>仅已安装程序有值：卸载/修改命令，来自注册表 UninstallString</summary>
    public string? UninstallCommand { get; set; }

    public bool IsShortcutFile => FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    /// <summary>提取自 Shell 的图标（32x32），懒加载</summary>
    public BitmapImage? Icon { get; set; }

    public override bool Equals(object? obj) =>
        obj is DesktopItem other && other.FullPath == FullPath;

    public override int GetHashCode() => FullPath.GetHashCode();
}
