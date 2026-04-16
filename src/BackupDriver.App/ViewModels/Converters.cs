using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BackupDriver.App.ViewModels;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive
                ? new SolidColorBrush(Color.Parse("#F44336"))
                : new SolidColorBrush(Color.Parse("#4CAF50"));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class LogLevelToColorConverter : IValueConverter
{
    public static readonly LogLevelToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level switch
            {
                "OK" => new SolidColorBrush(Color.Parse("#4CAF50")),
                "ERROR" => new SolidColorBrush(Color.Parse("#F44336")),
                "WARN" => new SolidColorBrush(Color.Parse("#FFA726")),
                _ => new SolidColorBrush(Color.Parse("#9E9E9E"))
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
