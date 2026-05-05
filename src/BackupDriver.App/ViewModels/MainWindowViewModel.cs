using Avalonia.Threading;
using BackupDriver.App.Messages;
using BackupDriver.Core.Services;
using BackupDriver.Infrastructure;
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
        IDialogService dialogService,
        ILicenseValidatorService licenseValidator,
        ILicenseStorage licenseStorage)
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

        // ─── Boot: Verificar licença PRO salva (ADR-005 / T3) ─────────────────
        // Tenta carregar a licença persistida e validar antes de qualquer interação do usuário.
        // Se válida, ativa o modo PRO imediatamente. Caso contrário, opera como FREE.
        InitializeLicenseState(licenseValidator, licenseStorage);

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

    /// <summary>
    /// Carrega a licença salva e inicializa IsProVersion no boot.
    /// Propaga o estado PRO para todos os ViewModels filhos (todos herdam ViewModelBase).
    /// </summary>
    private void InitializeLicenseState(ILicenseValidatorService validator, ILicenseStorage storage)
    {
        var savedKey = storage.Load();
        if (savedKey == null) return;

        var claims = validator.ValidateLicense(savedKey);
        var isPro = claims != null && claims.Tier == "PRO";

        // Propaga para todos os ViewModels filhos para que os bindings XAML funcionem
        SetProState(isPro);
    }

    /// <summary>
    /// Define o estado PRO em todos os ViewModels da aplicação.
    /// Chamado no boot e quando o usuário ativa uma nova licença.
    /// </summary>
    internal void SetProState(bool isPro)
    {
        IsProVersion       = isPro;
        Dashboard.IsProVersion  = isPro;
        Config.IsProVersion     = isPro;
        Schedule.IsProVersion   = isPro;
        Restore.IsProVersion    = isPro;
        Logs.IsProVersion       = isPro;
        RcloneConfig.IsProVersion = isPro;
        Healthcheck.IsProVersion  = isPro;
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        SelectedMenuItem = page;
        CurrentPage = page switch
        {
            "Dashboard"  => Dashboard,
            "Config"     => Config,
            "Schedule"   => Schedule,
            "Restore"    => Restore,
            "Logs"       => Logs,
            "RcloneConfig" => RcloneConfig,
            "Healthcheck"  => Healthcheck,
            _ => Dashboard
        };
    }
}
