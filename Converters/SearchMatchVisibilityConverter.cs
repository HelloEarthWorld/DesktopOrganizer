using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using DesktopOrganizer.Services;

namespace DesktopOrganizer.Converters;

/// <summary>没有搜索词，或名字包含搜索词 → 显示；否则隐藏（大小写不敏感）</summary>
public class SearchMatchVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var query = SearchState.Query;
        if (string.IsNullOrWhiteSpace(query)) return Visibility.Visible;
        var name = value as string ?? string.Empty;
        return name.Contains(query, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
