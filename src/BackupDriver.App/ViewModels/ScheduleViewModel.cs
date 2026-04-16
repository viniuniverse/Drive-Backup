using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using BackupDriver.Core.Config;
using BackupDriver.Core.Schedule;
using BackupDriver.App.Messages;

namespace BackupDriver.App.ViewModels;

/// <summary>
/// ViewModel da tela de Agendamento.
/// US-A5: usa InternalSchedulerService para controle do timer interno.
/// Expõe status em tempo real (Ativo/Pausado) e próximo disparo com countdown.
/// </summary>
public partial class ScheduleViewModel : ViewModelBase, IDisposable,
    IRecipient<SchedulerStatusMessage>
{
    private readonly IConfigManager _configManager;
    private readonly InternalSchedulerService _schedulerService;
    private DispatcherTimer? _displayTimer;
    private bool _isUpdatingFromConfig;

    [ObservableProperty]
    private bool _isIntervalMode = true;

    [ObservableProperty]
    private bool _isFixedTimeMode;

    [ObservableProperty]
    private int _intervalHours = 4;

    [ObservableProperty]
    private string _fixedTime = "02:00";

    private bool _isScheduleActive;
    public bool IsScheduleActive
    {
        get => _isScheduleActive;
        set
        {
            if (SetProperty(ref _isScheduleActive, value) && !_isUpdatingFromConfig)
            {
                SaveScheduleActiveAsync(value);
            }
        }
    }

    // US-A5: status do agendador interno (RN-A5.11)
    [ObservableProperty]
    private string _schedulerStatusIcon = "🔴";

    [ObservableProperty]
    private string _schedulerStatusText = "Serviço interno pausado";

    // US-A5: próximo disparo em tempo real (RN-A5.12)
    [ObservableProperty]
    private string _nextRunTime = "—";

    [ObservableProperty]
    private bool _canPause;

    [ObservableProperty]
    private bool _canResume;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Construtor recebe InternalSchedulerService diretamente (não a interface)
    // para acessar Pause/Resume/IsRunning/IsPaused/NextRunTime
    public ScheduleViewModel(IConfigManager configManager, InternalSchedulerService schedulerService)
    {
        _configManager = configManager;
        _schedulerService = schedulerService;
        WeakReferenceMessenger.Default.Register<SchedulerStatusMessage>(this);
        LoadConfig();
        StartDisplayTimer();
    }

    // Reage a mudanças de estado do agendador via mensagem reativa
    public void Receive(SchedulerStatusMessage message)
    {
        UpdateSchedulerStatus();
    }

    private void LoadConfig()
    {
        _isUpdatingFromConfig = true;
        var config = _configManager.GetConfig();
        IsIntervalMode = config.Schedule.Mode == "interval";
        IsFixedTimeMode = !IsIntervalMode;
        IntervalHours = config.Schedule.IntervalHours;
        FixedTime = config.Schedule.FixedTime;
        _isScheduleActive = config.Schedule.Active;
        OnPropertyChanged(nameof(IsScheduleActive));
        _isUpdatingFromConfig = false;
        UpdateSchedulerStatus();
    }

    private async void SaveScheduleActiveAsync(bool isActive)
    {
        var config = _configManager.GetConfig();
        config.Schedule.Active = isActive;
        config.Schedule.UseInternalScheduler = true;

        try
        {
            _configManager.SaveConfig(config);

            if (isActive)
            {
                var settings = new ScheduleSettings
                {
                    Mode = IsIntervalMode ? ScheduleMode.Interval : ScheduleMode.FixedTime,
                    IntervalHours = IntervalHours,
                    FixedTime = FixedTime,
                    Active = true
                };
                var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                await _schedulerService.ScheduleAsync(settings, appPath);
                StatusMessage = "✅ Agendamento ativado!";
            }
            else
            {
                _schedulerService.Stop();
                StatusMessage = "⏹️ Agendamento desativado.";
            }

            WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
            WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(isActive ? SchedulerStatus.Ok : SchedulerStatus.Inativo));
            UpdateSchedulerStatus();
        }
        catch (System.IO.IOException ex)
        {
            StatusMessage = $"❌ Erro ao salvar: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"❌ Acesso negado: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Erro: {ex.Message}";
        }

        ClearStatusMessageAfterDelay();
    }

    private async void ClearStatusMessageAfterDelay()
    {
        await Task.Delay(5000);
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Inicia um timer de UI que atualiza o status e o countdown a cada 30 segundos (RN-A5.12)
    /// </summary>
    private void StartDisplayTimer()
    {
        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _displayTimer.Tick += (_, _) => UpdateSchedulerStatus();
        _displayTimer.Start();

        // Atualiza imediatamente ao abrir a tela
        UpdateSchedulerStatus();
    }

    /// <summary>
    /// Atualiza status visual: ícone, texto e próximo disparo
    /// </summary>
    private void UpdateSchedulerStatus()
    {
        if (!_schedulerService.IsRunning)
        {
            SchedulerStatusIcon = "🔴";
            SchedulerStatusText = "Serviço interno inativo";
            CanPause = false;
            CanResume = false;
            NextRunTime = "Agendador não está ativo";
            return;
        }

        var isPaused = _schedulerService.IsPaused;
        SchedulerStatusIcon = isPaused ? "⏸️" : "🟢";
        SchedulerStatusText = isPaused ? "Serviço interno pausado" : "Serviço interno ativo";
        
        CanPause = !isPaused;
        CanResume = isPaused;

        if (_schedulerService.NextRunTime != DateTime.MinValue)
        {
            var nextRun = _schedulerService.NextRunTime;
            var timeToNext = ScheduleCalculator.FormatTimeSpan(nextRun, DateTime.Now);
            var prefix = isPaused ? "⏸️ Pausado: " : "";
            NextRunTime = $"{prefix}{nextRun:dd/MM/yyyy 'às' HH:mm} (em {timeToNext})";
        }
        else
        {
            NextRunTime = "Calculando próximo backup...";
        }
    }

    [RelayCommand]
    private void SelectIntervalMode()
    {
        IsIntervalMode = true;
        IsFixedTimeMode = false;
    }

    [RelayCommand]
    private void SelectFixedTimeMode()
    {
        IsIntervalMode = false;
        IsFixedTimeMode = true;
    }

    /// <summary>
    /// Pausa o agendador interno sem desativar a configuração (RN-A5.13)
    /// </summary>
    [RelayCommand]
    private void PauseScheduler()
    {
        _schedulerService.Pause();
        StatusMessage = "⏸️ Agendador pausado.";
        UpdateSchedulerStatus();

        // T1.2: Notifica Dashboard de mudança de estado
        WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Inativo));
    }

    /// <summary>
    /// Retoma o agendador interno a partir da configuração salva (RN-A5.14)
    /// </summary>
    [RelayCommand]
    private void ResumeScheduler()
    {
        // Se o serviço não estava rodando, inicia
        if (!_schedulerService.IsRunning)
            _schedulerService.Start();
        else
            _schedulerService.Resume();

        StatusMessage = "▶️ Agendador retomado.";
        UpdateSchedulerStatus();

        // T1.2: Notifica Dashboard de mudança de estado
        WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Ok));
    }

    /// <summary>
    /// Salva configuração e aplica ao agendador interno
    /// Task 4: SaveCommand agora só persiste campos de entrada (não mais booleanos de ativação)
    /// v1.6.2: ADR-B02 — Try-catch para evitar crash em falhas de I/O
    /// </summary>
    [RelayCommand]
    private async Task SaveScheduleAsync(CancellationToken token)
    {
        var config = _configManager.GetConfig();
        config.Schedule.Mode = IsIntervalMode ? "interval" : "fixed";
        config.Schedule.IntervalHours = IntervalHours;
        config.Schedule.FixedTime = FixedTime;
        config.Schedule.UseInternalScheduler = true;

        try
        {
            _configManager.SaveConfig(config);
            StatusMessage = "✅ Configurações salvas!";

            if (config.Schedule.Active)
            {
                var settings = new ScheduleSettings
                {
                    Mode = IsIntervalMode ? ScheduleMode.Interval : ScheduleMode.FixedTime,
                    IntervalHours = IntervalHours,
                    FixedTime = FixedTime,
                    Active = true
                };

                var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                await _schedulerService.ScheduleAsync(settings, appPath);
            }

            WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
            UpdateSchedulerStatus();
        }
        catch (System.IO.IOException ex)
        {
            StatusMessage = $"❌ Erro ao salvar: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"❌ Acesso negado: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Erro: {ex.Message}";
        }

        ClearStatusMessageAfterDelay();
    }

    public void Dispose()
    {
        _displayTimer?.Stop();
    }
}
