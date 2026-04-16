using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BackupDriver.App.Views;

public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? Brushes.LimeGreen : Brushes.OrangeRed;
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
