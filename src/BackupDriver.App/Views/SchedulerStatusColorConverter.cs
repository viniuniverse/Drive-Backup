using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BackupDriver.App.Views;

/// <summary>
/// T1.3: Conversor de cor dinâmico para o status do agendador.
/// "Rodando" -> Verde, "Pausado" -> Vermelho, "Parado" -> Cinza
/// </summary>
public class SchedulerStatusColorConverter : IValueConverter
{
    public static readonly SchedulerStatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Ok" => new SolidColorBrush(Color.Parse("#81C784")),
                "Inativo" => new SolidColorBrush(Color.Parse("#F44336")),
                _ => new SolidColorBrush(Color.Parse("#999999"))
            };
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
