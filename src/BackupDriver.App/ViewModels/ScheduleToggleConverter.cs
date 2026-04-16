using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace BackupDriver.App.ViewModels;

public class ScheduleToggleConverter : IValueConverter
{
    public static readonly ScheduleToggleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? "🔴 Desativar Agendamento" : "🟢 Ativar Agendamento";
        }
        return "Toggle";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
