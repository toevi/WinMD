using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinMD;

/// <summary>bool → Visibility (WinUI 3 nie ma wbudowanego). Parametr "invert" odwraca.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool flag = value is bool b && b;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility v && v == Visibility.Visible;
}
