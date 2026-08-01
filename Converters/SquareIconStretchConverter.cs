using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using DesktopOrganizer.Services;

namespace DesktopOrganizer.Converters;

/// <summary>
/// 全局圆角开关开着 → UniformToFill（所有图标裁切填满，macOS 应用图标风格）
/// 关 → Uniform（完整显示，不裁切）
/// </summary>
public class SquareIconStretchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => AppSettingsService.RoundedIconsEnabled ? Stretch.UniformToFill : Stretch.Uniform;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
