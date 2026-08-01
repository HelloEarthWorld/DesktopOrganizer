using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DesktopOrganizer.Converters;

/// <summary>数量 = 0 时显示（用于空分组的"拖东西到这里"提示），否则隐藏</summary>
public class EmptyCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is int count && count == 0) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
