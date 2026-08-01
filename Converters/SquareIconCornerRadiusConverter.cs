using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using DesktopOrganizer.Services;

namespace DesktopOrganizer.Converters;

/// <summary>全局圆角开关开着 → 圆角10（所有图标统一，macOS 风格）；关 → 方角（原图完整显示）</summary>
public class SquareIconCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => AppSettingsService.RoundedIconsEnabled ? new CornerRadius(10) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
