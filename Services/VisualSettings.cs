using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DesktopOrganizer.Services;

/// <summary>
/// 按主题提供卡片背景画笔：透明度做在颜色 alpha 上而不是元素 Opacity 上，
/// 这样只淡背景，卡片里的图标文字保持清晰。
/// 浅色模式下更透一点（浅色背景对比弱，不调低会感觉不到亚克力效果），深色保持原值。
/// 搜索结果面板是文字列表，必须保证可读性，用独立、更不透明的画笔。
/// </summary>
public static class VisualSettings
{
    public static Brush CardBrush { get; private set; } = CreateBrush(0.7, dark: true);

    public static Brush SearchPanelBrush { get; private set; } = CreateBrush(0.9, dark: true);

    public static void ApplyTheme(ElementTheme theme)
    {
        var dark = theme == ElementTheme.Dark;
        CardBrush = CreateBrush(dark ? 0.7 : 0.25, dark);
        SearchPanelBrush = CreateBrush(dark ? 0.85 : 0.9, dark);
    }

    private static Brush CreateBrush(double opacity, bool dark)
    {
        var color = dark
            ? Color.FromArgb((byte)(opacity * 255), 44, 44, 44)
            : Color.FromArgb((byte)(opacity * 255), 253, 253, 253);
        return new SolidColorBrush(color);
    }
}
