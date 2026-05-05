using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace BackupDriver.App.ViewModels;

/// <summary>
/// Converte o estado booleano IsProVersion para um badge de texto visual.
/// Usado na ConfigView para indicar se a versão PRO está ativa ou não.
/// </summary>
public class ProVersionBadgeConverter : IValueConverter
{
    // Singleton — padrão utilizado pelos outros converters do projeto
    public static readonly ProVersionBadgeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Retorna badge dourado para PRO ou texto simples para FREE
        return value is true ? "⭐ PRO" : "🔓 FREE";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
