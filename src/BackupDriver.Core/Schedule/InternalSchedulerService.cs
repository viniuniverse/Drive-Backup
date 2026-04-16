using System;
using System.Threading;
using System.Threading.Tasks;
using BackupDriver.Core.Backup;
using BackupDriver.Core.Config;
using CommunityToolkit.Mvvm.Messaging;

namespace BackupDriver.Core.Schedule;

/// <summary>
/// Motor unificado de agendamento e execução de backup in-app.
/// v1.3.2: Elimina dependência do Worker Service externo;
/// o próprio serviço dispara o IBackupOrchestrator em background
/// quando a data/hora programada é atingida.
/// </summary>
public class InternalSchedulerService : ISchedulerService, IDisposable
{
    private readonly IConfigManager _configManager;
    private readonly Infrastructure.ILogger _logger;
    private readonly IBackupOrchestrator _orchestrator;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    // ── Estado do agendador ──
    public bool IsRunning { get; private set; }
    public DateTime NextRunTime { get; private set; }
    public bool IsPaused { get; private set; }

    // ── Controle de execução concorrente (Tarefa 2) ──
    private bool _isBackupRunning;

    // ── Baseline Time Lock — captura tempo-base estático na ativação (Tarefa 2) ──
    private DateTime _baseTimeLock = DateTime.Now;

    // ── Assinatura relacional de config para detectar mudanças (Tarefa 3) ──
    private string _lastConfigSig = string.Empty;

    public event Func<Task>? OnScheduledBackup;

    public InternalSchedulerService(
        IConfigManager configManager,
        Infrastructure.ILogger logger,
        IBackupOrchestrator orchestrator)
    {
        _configManager = configManager;
        _logger = logger;
        _orchestrator = orchestrator;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _baseTimeLock = DateTime.Now;
        _logger.Info("InternalSchedulerService: Motor unificado iniciado (v1.3.2).");

        _ = RunLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Loop principal: polling de 1 segundo.
    /// Detecta quando NextRunTime é atingido e dispara backup via Fire-and-Forget.
    /// Não lê state.json; tudo in-process.
    /// </summary>
    private async Task RunLoopAsync(CancellationToken ct)
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        // Inicializa assinatura de config
        var config = _configManager.GetConfig();
        _lastConfigSig = BuildConfigSignature(config);

        // Le LastRun persistido para não perder referência ao reiniciar
        if (config.Schedule.LastRun != DateTime.MinValue)
        {
            _baseTimeLock = config.Schedule.LastRun;
        }

        try
        {
            while (await _timer.WaitForNextTickAsync(ct))
            {
                config = _configManager.GetConfig();

                // ── Scheduler inativo? ──
                if (!config.Schedule.Active)
                {
                    IsPaused = true;
                    NextRunTime = DateTime.MinValue;
                    WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Inativo));
                    continue;
                }

                // Config ativo — garante que IsPaused reflete estado real
                IsPaused = false;

                // ── Detecção de mudança de config via assinatura (Tarefa 3) ──
                string currentSig = BuildConfigSignature(config);
                if (currentSig != _lastConfigSig)
                {
                    // Config mudou — recaptura baseTime para novo ciclo de cálculo
                    _baseTimeLock = DateTime.Now;
                    _lastConfigSig = currentSig;
                    _logger.Info($"Scheduler: Config alterada, baseTime recalculado: {_baseTimeLock}");
                }

                // ── Calcula próximo disparo baseado em LastRun ou baseTimeLock ──
                DateTime referenceTime = config.Schedule.LastRun != DateTime.MinValue
                    ? config.Schedule.LastRun
                    : _baseTimeLock;

                NextRunTime = ScheduleCalculator.CalculateNextRun(config.Schedule, referenceTime);

                // ── Se backup já rodando, apenas emite status de execução ──
                if (_isBackupRunning)
                {
                    WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Executando));
                    continue;
                }

                // ── Avaliação do gatilho de disparo (Tarefa 3) ──
                if (NextRunTime != DateTime.MinValue && DateTime.Now >= NextRunTime && !_isBackupRunning)
                {
                    _logger.Info($"Scheduler: Gatilho acionado! NextRun={NextRunTime:HH:mm:ss} | Agora={DateTime.Now:HH:mm:ss}");
                    // Fire-and-Forget: não bloqueia o loop de polling
                    _ = ExecuteBackgroundBackupFireAndForget(config, ct);
                }
                else
                {
                    // Tudo ok, apenas reporta status ativo
                    WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Ok));
                }
            }
        }
        catch (OperationCanceledException) { /* Shutdown gracioso */ }
    }

    /// <summary>
    /// Executa o backup em background de forma assíncrona (Fire-and-Forget).
    /// Tarefa 4: Invoca o IBackupOrchestrator e notifica VMs via mensageria reativa.
    /// </summary>
    private async Task ExecuteBackgroundBackupFireAndForget(AppConfig config, CancellationToken mainToken)
    {
        // Trava para evitar disparo paralelo repetido
        _isBackupRunning = true;

        try
        {
            // Sinaliza para o Dashboard que backup está em execução
            WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Executando));
            _logger.Info("Scheduler: Backup automático iniciado.");

            // Executa o orquestrador com callback de progresso via mensageiro
            var result = await _orchestrator.ExecuteBackupAsync(config, step =>
            {
                // Emite progresso de cada passo para a UI via status
                _logger.Info($"Scheduler Passo {step.Number}: {step.Name} — {step.Status}");
            }, mainToken);

            // Atualiza LastRun e persiste no config.json
            config.Schedule.LastRun = DateTime.Now;
            _configManager.SaveConfig(config);
            _logger.Info($"Scheduler: LastRun atualizado para {config.Schedule.LastRun:dd/MM/yyyy HH:mm:ss}");

            // Envia mensagem reativa de conclusão para DashboardViewModel
            WeakReferenceMessenger.Default.Send(
                new BackgroundBackupCompletedMessage(result));

            if (result.Success)
            {
                _logger.Ok($"Scheduler: Backup automático concluído com sucesso — {result.OutputFile}");
            }
            else
            {
                _logger.Error($"Scheduler: Backup automático falhou — {result.ErrorMessage}");
            }

            // Recalcula próximo disparo baseado no novíssimo LastRun
            NextRunTime = ScheduleCalculator.CalculateNextRun(config.Schedule, config.Schedule.LastRun);
            _logger.Info($"Scheduler: Próximo backup agendado para {NextRunTime:dd/MM/yyyy HH:mm:ss}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Scheduler: Erro crítico no backup automático: {ex.Message}");

            // Envia resultado de erro para a UI
            WeakReferenceMessenger.Default.Send(
                new BackgroundBackupCompletedMessage(new BackupResult { ErrorMessage = ex.Message }));
        }
        finally
        {
            // Libera a trava — permite próximo ciclo de disparo
            _isBackupRunning = false;
            WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Ok));
        }
    }

    /// <summary>
    /// Gera assinatura textual da config do agendamento para detecção de mudanças.
    /// </summary>
    private static string BuildConfigSignature(AppConfig config)
    {
        return $"{config.Schedule.Mode}|{config.Schedule.Active}|{config.Schedule.FixedTime}|{config.Schedule.IntervalHours}";
    }

    // ── Comandos da interface (Pause/Resume/Stop) ──

    public void Pause()
    {
        var config = _configManager.GetConfig();
        config.Schedule.Active = false;
        _configManager.SaveConfig(config);
        _logger.Info("Scheduler: Pausado pela UI.");
        WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Inativo));
    }

    public void Resume()
    {
        var config = _configManager.GetConfig();
        config.Schedule.Active = true;
        _configManager.SaveConfig(config);
        _logger.Info("Scheduler: Retomado pela UI.");
        WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Ok));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        IsRunning = false;
        NextRunTime = DateTime.MinValue;
        _logger.Info("Scheduler: Motor parado.");
        WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Inativo));
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _timer?.Dispose();
    }

    // ── Interface ISchedulerService ──

    public Task<bool> ScheduleAsync(ScheduleSettings settings, string appPath)
    {
        var config = _configManager.GetConfig();
        config.Schedule.Mode = settings.Mode == ScheduleMode.Interval ? "interval" : "fixed";
        config.Schedule.IntervalHours = settings.IntervalHours;
        config.Schedule.FixedTime = settings.FixedTime;
        config.Schedule.Active = true;

        _configManager.SaveConfig(config);

        // Recalcula assinatura para que o loop detecte a mudança
        _lastConfigSig = BuildConfigSignature(config);
        _baseTimeLock = DateTime.Now;

        if (!IsRunning) Start();
        return Task.FromResult(true);
    }

    public Task<bool> RemoveScheduleAsync()
    {
        var config = _configManager.GetConfig();
        config.Schedule.Active = false;
        _configManager.SaveConfig(config);

        WeakReferenceMessenger.Default.Send(new SchedulerStatusMessage(SchedulerStatus.Inativo));
        return Task.FromResult(true);
    }

    public Task<DateTime?> GetNextRunTimeAsync()
    {
        return Task.FromResult<DateTime?>(IsRunning && NextRunTime != DateTime.MinValue ? NextRunTime : null);
    }

    public bool IsScheduled()
    {
        var config = _configManager.GetConfig();
        return config.Schedule.Active;
    }
}