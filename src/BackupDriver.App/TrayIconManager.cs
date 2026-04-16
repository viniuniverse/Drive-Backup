using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace BackupDriver.App;

/// <summary>
/// Gerencia o ícone do BackupDriver na bandeja do sistema (System Tray).
/// Responsável por: exibir/ocultar ícone, menu de contexto e notificações balloon.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly TrayIcon _trayIcon;
    private Window? _mainWindow;
    private bool _isExiting;

    // Callbacks para ações do menu tray
    public Func<Task>? OnBackupRequested { get; set; }
    public Action? OnExitRequested { get; set; }

    public TrayIconManager()
    {
        // Cria o ícone da bandeja com menu de contexto
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Backup Driver V1",
            IsVisible = false,
            Icon = new WindowIcon(LoadIconBitmap())
        };

        // Duplo clique no ícone restaura a janela
        _trayIcon.Clicked += OnTrayIconClicked;

        // Menu de contexto com 3 opções conforme RN-A5.10
        var menu = new NativeMenu();

        var backupItem = new NativeMenuItem("▶️ Executar Backup Agora");
        backupItem.Click += async (_, _) =>
        {
            if (OnBackupRequested != null)
                await OnBackupRequested.Invoke();
        };

        var openItem = new NativeMenuItem("🖥️ Abrir");
        openItem.Click += (_, _) => RestoreWindow();

        var separator = new NativeMenuItemSeparator();

        var exitItem = new NativeMenuItem("❌ Sair");
        exitItem.Click += (_, _) =>
        {
            _isExiting = true;
            OnExitRequested?.Invoke();
        };

        menu.Items.Add(backupItem);
        menu.Items.Add(openItem);
        menu.Items.Add(separator);
        menu.Items.Add(exitItem);

        _trayIcon.Menu = menu;
    }

    /// <summary>
    /// Inicializa o gerenciador com referência da janela principal.
    /// Deve ser chamado após a MainWindow ser criada.
    /// </summary>
    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        // Intercepta o evento de fechamento — em vez de fechar, minimiza para a bandeja (RN-A5.7)
        _mainWindow.Closing += OnWindowClosing;
    }

    /// <summary>
    /// Intercepta o fechamento da janela — oculta em vez de encerrar (RN-A5.7)
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Se está saindo pelo menu tray (❌ Sair), permite o fechamento real
        if (_isExiting) return;

        // Cancela o fechamento e oculta a janela
        e.Cancel = true;
        HideWindow();
    }

    /// <summary>
    /// Oculta a janela principal e exibe o ícone na bandeja (RN-A5.7)
    /// </summary>
    public void HideWindow()
    {
        _mainWindow?.Hide();
        ShowIcon();
    }

    /// <summary>
    /// Restaura a janela principal a partir da bandeja (RN-A5.9)
    /// </summary>
    public void RestoreWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.BringIntoView();
        HideIcon();
    }

    /// <summary>
    /// Exibe o ícone na bandeja do sistema
    /// </summary>
    public void ShowIcon() => _trayIcon.IsVisible = true;

    /// <summary>
    /// Oculta o ícone da bandeja do sistema
    /// </summary>
    public void HideIcon() => _trayIcon.IsVisible = false;

    /// <summary>
    /// Exibe notificação balloon na bandeja (RN-A5.5).
    /// Nota: Avalonia 11.x suporta via TrayIcon.ShowNotification em versões futuras.
    /// Implementação atual usa ToolTipText dinâmico como fallback.
    /// </summary>
    public void ShowNotification(string title, string message)
    {
        // Atualiza tooltip com resultado do backup
        _trayIcon.ToolTipText = $"{title} — {message}";

        // Restaura tooltip padrão após 10 segundos
        System.Threading.Tasks.Task.Delay(10000).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _trayIcon.ToolTipText = "Backup Driver V1";
            });
        });
    }

    /// <summary>
    /// Retorna true se o processo está saindo voluntariamente
    /// </summary>
    public bool IsExiting => _isExiting;

    /// <summary>
    /// Duplo clique no ícone da bandeja restaura a janela (RN-A5.9)
    /// </summary>
    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        RestoreWindow();
    }

    /// <summary>
    /// Carrega o bitmap do ícone. Usa ícone padrão do app se disponível.
    /// </summary>
    private static Avalonia.Media.Imaging.Bitmap LoadIconBitmap()
    {
        // Tenta carregar ícone embutido nos assets
        try
        {
            var assets = Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://BackupDriver.App/Assets/backup-driver.ico"));
            return new Avalonia.Media.Imaging.Bitmap(assets);
        }
        catch
        {
            // Fallback: cria bitmap vazio 16x16 (ícone invisível mas funcional)
            return CreateFallbackBitmap();
        }
    }

    /// <summary>
    /// Cria bitmap de fallback simples caso o ícone não esteja disponível
    /// </summary>
    private static Avalonia.Media.Imaging.Bitmap CreateFallbackBitmap()
    {
        // Cria um bitmap 32x32 com cor sólida como placeholder
        var renderTarget = new Avalonia.Media.Imaging.RenderTargetBitmap(
            new Avalonia.PixelSize(32, 32), new Avalonia.Vector(96, 96));

        using var ctx = renderTarget.CreateDrawingContext();
        ctx.DrawRectangle(
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
            null,
            new Avalonia.Rect(0, 0, 32, 32));

        return renderTarget;
    }

    public void Dispose()
    {
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }
}
