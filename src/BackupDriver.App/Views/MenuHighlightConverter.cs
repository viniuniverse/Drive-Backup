using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BackupDriver.App.Views;

public class MenuHighlightConverter : IValueConverter
{
    public static readonly MenuHighlightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selected && parameter is string menuItem)
        {
            return selected == menuItem
                ? new SolidColorBrush(Color.Parse("#3A3A5C"))
                : new SolidColorBrush(Colors.Transparent);
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
