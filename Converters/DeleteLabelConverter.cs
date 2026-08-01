using Microsoft.UI.Xaml.Data;

namespace DesktopOrganizer.Converters;

public class DeleteLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => (value is bool b && b) ? "删除此快捷方式" : "删除";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
