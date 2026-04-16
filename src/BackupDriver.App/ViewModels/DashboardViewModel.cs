using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using BackupDriver.Core.Backup;
using BackupDriver.Core.Config;
using BackupDriver.Core.Schedule;
// v1.3.2: BackgroundBackupCompletedMessage agora em Core.Schedule (não mais em App.Messages)
using BackupDriver.App.Messages; // ConfigChangedMessage ainda reside aqui

namespace BackupDriver.App.ViewModels;

/// <summary>
/// ViewModel do Dashboard principal.
/// v1.6: Atualizado com Google Drive widget, ProgressBar, Footer créditos, label ZIP
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IDisposable,
    IRecipient<ModuleStatusChangedMessage>,
    IRecipient<ConfigChangedMessage>,
    IRecipient<SchedulerStatusMessage>,
    IRecipient<BackgroundBackupCompletedMessage>
{
    private readonly IBackupOrchestrator _backupOrchestrator;
    private readonly IConfigManager _configManager;
    private readonly InternalSchedulerService _scheduler;

    // Timer de UI para atualizar o próximo backup em tempo real
    private DispatcherTimer? _refreshTimer;

    [ObservableProperty]
    private string _lastBackupDate = "Nenhum backup realizado";

    [ObservableProperty]
    private string _lastBackupStatus = "—";

    [ObservableProperty]
    private string _lastBackupFile = "—";

    [ObservableProperty]
    private string _lastBackupDuration = "—";

    // US-A5: atualizado em tempo real pelo InternalSchedulerService (RN-A5.12)
    [ObservableProperty]
    private string _nextBackupTime = "—";

    [ObservableProperty]
    private bool _isBackupRunning;

    // v1.6.1: ADR-H03 — Progresso real do backup
    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _showRealProgress;

    // US-A3: IsConfigComplete controla visibilidade do botão de backup manual (RN-A3.1)
    [ObservableProperty]
    private bool _isConfigComplete;

    // T2.1: Status das integrações externas
    [ObservableProperty]
    private bool _isGoogleDriveActive;

    public string GoogleDriveStatus => IsGoogleDriveActive ? "Habilitado" : "Desabilitado";

    [ObservableProperty]
    private bool _isHealthcheckActive;

    public string HealthcheckStatus => IsHealthcheckActive ? "Habilitado" : "Desabilitado";

    // T3.1: Estado textual do agendador (Running/Stopped)
    [ObservableProperty]
    private string _schedulerStatus = "Parado";

    // v1.6: T-1.6.26 — Propriedades do widget Google Drive
    [ObservableProperty]
    private string _lastCloudUploadDate = "Nunca";

    [ObservableProperty]
    private string _clientName = "—";

    [ObservableProperty]
    private string _cloudStatus = "Não configurado";

    // v1.6: T-1.6.08 — Label atualizado de "7-Zip" para "ZIP"
    [ObservableProperty]
    private BackupStep[] _steps = new[]
    {
        new BackupStep { Number = 1, Name = "Backup Firebird" },
        new BackupStep { Number = 2, Name = "Comprimir (ZIP)" },
        new BackupStep { Number = 3, Name = "Upload Drive" },
        new BackupStep { Number = 4, Name = "Healthcheck" }
    };

    private CancellationTokenSource? _cts;

    public DashboardViewModel(
        IBackupOrchestrator backupOrchestrator,
        IConfigManager configManager,
        InternalSchedulerService scheduler)
    {
        _backupOrchestrator = backupOrchestrator;
        _configManager = configManager;
        _scheduler = scheduler;
        CheckConfig();
        StartRefreshTimer();

        // T1.2: Registrar para receber mensagens reativas
        WeakReferenceMessenger.Default.Register<ModuleStatusChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<ConfigChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<SchedulerStatusMessage>(this);
        WeakReferenceMessenger.Default.Register<BackgroundBackupCompletedMessage>(this);
    }

    // T1.2: Reage a mudanças de configuração (Sincronização persistente)
    public void Receive(ConfigChangedMessage message)
    {
        CheckConfig();
    }

    // v1.6.5: Reage a mudanças de UI em tempo real (Pre-Save)
    public void Receive(ModuleStatusChangedMessage message)
    {
        if (message.ModuleName == "Healthcheck")
        {
            IsHealthcheckActive = message.IsEnabled;
            OnPropertyChanged(nameof(HealthcheckStatus));
        }
        else if (message.ModuleName == "GoogleDrive")
        {
            IsGoogleDriveActive = message.IsEnabled;
            OnPropertyChanged(nameof(GoogleDriveStatus));
        }
    }

    // T1.2: Reage a mudanças de estado do agendador
    public void Receive(SchedulerStatusMessage message)
    {
        RefreshNextRunTime();
    }

    // T1.2: Reage a conclusão de backup automático em background
    public void Receive(BackgroundBackupCompletedMessage message)
    {
        var result = message.Value;
        LastBackupDate = DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm");

        if (result.Success)
        {
            LastBackupStatus = "Sucesso";
            LastBackupFile = System.IO.Path.GetFileName(result.OutputFile);
            LastBackupDuration = $"{result.Duration.TotalMinutes:F1}min";
        }
        else
        {
            LastBackupStatus = "Falha";
            LastBackupFile = result.ErrorMessage;
        }

        // v1.6: Atualizar widget Google Drive após backup
        LoadCloudInfo();
    }

    /// <summary>
    /// Verifica se a configuração está completa para exibir o botão de backup (RN-A3.1)
    /// </summary>
    public void CheckConfig()
    {
        var config = _configManager.GetConfig();
        IsConfigComplete =
            !string.IsNullOrEmpty(config.Firebird.DbPath) &&
            !string.IsNullOrEmpty(config.Firebird.InstallPath) &&
            !string.IsNullOrEmpty(config.Backup.OutputPath);

        // T2.1: Atualiza status do Dashboard
        IsGoogleDriveActive = config.Rclone.Enabled;
        IsHealthcheckActive = config.Healthcheck.Enabled;

        OnPropertyChanged(nameof(GoogleDriveStatus));
        OnPropertyChanged(nameof(HealthcheckStatus));

        // v1.6: Carregar informações do Google Drive
        LoadCloudInfo();
    }

    // v1.6: T-1.6.26 — Carrega dados do widget Google Drive a partir do config
    private void LoadCloudInfo()
    {
        var config = _configManager.GetConfig();

        ClientName = string.IsNullOrEmpty(config.Rclone.ClientName) ? "—" : config.Rclone.ClientName;

        if (config.Rclone.LastCloudUploadDate != DateTime.MinValue)
            LastCloudUploadDate = config.Rclone.LastCloudUploadDate.ToString("dd/MM/yyyy 'às' HH:mm");
        else
            LastCloudUploadDate = "Nunca";

        if (!config.Rclone.Configured)
            CloudStatus = "Não configurado";
        else if (!config.Rclone.Enabled)
            CloudStatus = "Inativo";
        else
            CloudStatus = "Ativo";
    }

    /// <summary>
    /// Inicia timer de UI para atualizar NextBackupTime em tempo real (Task 5.9)
    /// </summary>
    private void StartRefreshTimer()
    {
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60) // Atualiza a cada 1 minuto
        };
        _refreshTimer.Tick += (_, _) => RefreshNextRunTime();
        _refreshTimer.Start();
        RefreshNextRunTime(); // Atualiza imediatamente
    }

    /// <summary>
    /// Lê NextRunTime do InternalSchedulerService e formata para exibição
    /// </summary>
    private void RefreshNextRunTime()
    {
        if (!_scheduler.IsRunning)
        {
            NextBackupTime = "Agendador Inativo";
            SchedulerStatus = "Inativo";
            return;
        }

        SchedulerStatus = _scheduler.IsPaused ? "Pausado" : "Ok";

        if (_scheduler.NextRunTime != DateTime.MinValue)
        {
            var nextRun = _scheduler.NextRunTime;
            var timeLeft = ScheduleCalculator.FormatTimeSpan(nextRun, DateTime.Now);
            var prefix = _scheduler.IsPaused ? "⏸️ Pausado: " : "";
            NextBackupTime = $"{prefix}{nextRun:dd/MM/yyyy 'às' HH:mm} (em {timeLeft})";
        }
        else
        {
            NextBackupTime = "Calculando próximo backup...";
        }
    }

    /// <summary>
    /// Executa backup manual imediato (RN-A3.2 + RN-A3.3) — botão já existia
    /// </summary>
    [RelayCommand]
    private async Task ExecuteBackupAsync(CancellationToken token)
    {
        if (IsBackupRunning) return;

        IsBackupRunning = true;
        ProgressValue = 0;
        ShowRealProgress = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);

        ResetSteps();

        var config = _configManager.GetConfig();

        var result = await _backupOrchestrator.ExecuteBackupAsync(
            config,
            step =>
            {
                var index = step.Number - 1;
                Steps[index] = step;

                // v1.6.1: ADR-H03 — Calcular progresso baseado na etapa
                ProgressValue = step.Number switch
                {
                    1 => step.IsCompleted ? 25 : 10,
                    2 => step.IsCompleted ? 60 : 35,
                    3 => step.IsCompleted ? 90 : 70,
                    4 => step.IsCompleted ? 100 : 95,
                    _ => 0
                };
            },
            _cts.Token
        );

        if (result.Success)
        {
            LastBackupDate = DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm");
            LastBackupStatus = "✅ Sucesso";
            LastBackupFile = System.IO.Path.GetFileName(result.OutputFile);
            LastBackupDuration = $"{result.Duration.TotalMinutes:F1}min";
        }
        else
        {
            LastBackupDate = DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm");
            LastBackupStatus = "❌ Falha";
            LastBackupFile = result.ErrorMessage;
        }

        // v1.6: Atualizar widget Google Drive após backup manual
        LoadCloudInfo();

        IsBackupRunning = false;
        ProgressValue = 0;
        ShowRealProgress = false;
    }

    [RelayCommand]
    private void CancelBackup()
    {
        _cts?.Cancel();
    }

    // v1.6: T-1.6.27 — Comandos para abrir links do desenvolvedor no navegador
    [RelayCommand]
    private void OpenLinkedIn()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.linkedin.com/in/viniciusgadelha/",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/viniuniverse",
            UseShellExecute = true
        });
    }

    private void ResetSteps()
    {
        for (int i = 0; i < Steps.Length; i++)
        {
            Steps[i].Status = "Aguardando...";
            Steps[i].IsCompleted = false;
            Steps[i].IsFailed = false;
            Steps[i].Progress = null;
        }
        ProgressValue = 0;
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
    }
}
