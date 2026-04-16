using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using BackupDriver.Core.Services;

namespace BackupDriver.App.Services;

public class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info)
    {
        var icon = type switch
        {
            DialogType.Success => "✅",
            DialogType.Error => "❌",
            DialogType.Warning => "⚠️",
            _ => "ℹ️"
        };

        var accentColor = type switch
        {
            DialogType.Success => "#4CAF50",
            DialogType.Error => "#F44336",
            DialogType.Warning => "#FFA726",
            _ => "#2196F3"
        };

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#2D2D3D")),
            SystemDecorations = SystemDecorations.Full,
            Topmost = true
        };

        var contentPanel = new StackPanel
        {
            Spacing = 15,
            Margin = new Avalonia.Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleBlock = new TextBlock
        {
            Text = $"{icon} {title}",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse(accentColor)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            Height = 35,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse(accentColor)),
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(4)
        };

        var tcs = new TaskCompletionSource();

        okButton.Click += (_, _) =>
        {
            tcs.TrySetResult();
            dialog.Close();
        };

        contentPanel.Children.Add(titleBlock);
        contentPanel.Children.Add(messageBlock);
        contentPanel.Children.Add(okButton);

        dialog.Content = contentPanel;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var owner = desktop.MainWindow;
            if (owner != null)
                dialog.ShowDialog(owner);
            else
                dialog.Show();
        }
        else
        {
            dialog.Show();
        }

        await tcs.Task;
    }

    /// <summary>
    /// Exibe popup de "Nova versão disponível" com dois botões.
    /// "Baixar agora" abre o browser na página de releases do GitHub.
    /// "Agora não" fecha o dialog e o app continua normalmente.
    /// </summary>
    public async Task ShowUpdateDialogAsync(string newVersion, string releaseUrl)
    {
        // Dialog de atualização com visual diferenciado (cor azul vibrante)
        var dialog = new Window
        {
            Title = "Atualização Disponível",
            Width = 420,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1E1E2E")),
            SystemDecorations = SystemDecorations.Full,
            Topmost = true
        };

        var contentPanel = new StackPanel
        {
            Spacing = 16,
            Margin = new Avalonia.Thickness(24),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Título com ícone de foguete
        var titleBlock = new TextBlock
        {
            Text = "🚀 Nova versão disponível!",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#7C9EFF")),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Mensagem com a versão encontrada
        var messageBlock = new TextBlock
        {
            Text = $"A versão {newVersion} está disponível.\nDeseja ir para a página de download?",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        // Painel com os dois botões lado a lado
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var downloadButton = new Button
        {
            Content = "⬇️ Baixar agora",
            Width = 140,
            Height = 36,
            Background = new SolidColorBrush(Color.Parse("#7C9EFF")),
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(5)
        };

        var cancelButton = new Button
        {
            Content = "Agora não",
            Width = 110,
            Height = 36,
            Background = new SolidColorBrush(Color.Parse("#3A3A50")),
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            FontSize = 13,
            CornerRadius = new CornerRadius(5)
        };

        var tcs = new TaskCompletionSource();

        // "Baixar agora": abre o browser na página de releases do GitHub
        downloadButton.Click += (_, _) =>
        {
            try
            {
                // UseShellExecute=true é obrigatório para abrir URLs no browser padrão
                Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
            }
            catch { /* falha silenciosa se o browser não abrir */ }

            tcs.TrySetResult();
            dialog.Close();
        };

        // "Agora não": apenas fecha o dialog
        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult();
            dialog.Close();
        };

        buttonsPanel.Children.Add(downloadButton);
        buttonsPanel.Children.Add(cancelButton);

        contentPanel.Children.Add(titleBlock);
        contentPanel.Children.Add(messageBlock);
        contentPanel.Children.Add(buttonsPanel);

        dialog.Content = contentPanel;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var owner = desktop.MainWindow;
            if (owner != null)
                await dialog.ShowDialogAsync(owner);
            else
                dialog.Show();
        }
        else
        {
            dialog.Show();
        }

        await tcs.Task;
    }
}