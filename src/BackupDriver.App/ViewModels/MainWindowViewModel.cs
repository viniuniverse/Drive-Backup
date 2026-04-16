using Avalonia.Threading;
using BackupDriver.App.Messages;
using BackupDriver.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BackupDriver.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _selectedMenuItem = "Dashboard";

    public DashboardViewModel Dashboard { get; }
    public ConfigViewModel Config { get; }
    public ScheduleViewModel Schedule { get; }
    public RestoreViewModel Restore { get; }
    public LogsViewModel Logs { get; }
    public RcloneConfigViewModel RcloneConfig { get; }
    public HealthcheckViewModel Healthcheck { get; }

    // Referência ao DialogService para exibir o popup de atualização
    private readonly IDialogService _dialogService;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        ConfigViewModel config,
        ScheduleViewModel schedule,
        RestoreViewModel restore,
        LogsViewModel logs,
        RcloneConfigViewModel rcloneConfig,
        HealthcheckViewModel healthcheck,
        IDialogService dialogService)
    {
        Dashboard = dashboard;
        Config = config;
        Schedule = schedule;
        Restore = restore;
        Logs = logs;
        RcloneConfig = rcloneConfig;
        Healthcheck = healthcheck;
        _dialogService = dialogService;
        _currentPage = Dashboard;

        // Registra listener para mensagem de nova versão disponível.
        // Executado fire-and-forget na thread de UI para evitar bloqueio do Messenger.
        WeakReferenceMessenger.Default.Register<UpdateAvailableMessage>(this, (_, msg) =>
        {
            // Dispatcher garante que o dialog Avalonia rode na thread da UI
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await _dialogService.ShowUpdateDialogAsync(msg.NewVersion, msg.ReleaseUrl);
            });
        });
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        SelectedMenuItem = page;
        CurrentPage = page switch
        {
            "Dashboard" => Dashboard,
            "Config" => Config,
            "Schedule" => Schedule,
            "Restore" => Restore,
            "Logs" => Logs,
            "RcloneConfig" => RcloneConfig,
            "Healthcheck" => Healthcheck,
            _ => Dashboard
        };
    }
}

