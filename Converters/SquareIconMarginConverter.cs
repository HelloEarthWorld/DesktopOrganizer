using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using DesktopOrganizer.Services;

namespace DesktopOrganizer.Converters;

/// <summary>
/// 圆角开 → 5px 内边距（先缩小再裁切，macOS 应用图标风格，所有图标统一）；
/// 圆角关 → 0 边距，原图完整显示不缩小。
/// </summary>
public class SquareIconMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => AppSettingsService.RoundedIconsEnabled ? new Thickness(5) : new Thickness(0);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
