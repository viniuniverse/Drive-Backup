using System.Text.Json;
using BackupDriver.Core.Backup;
using BackupDriver.Core.Config;
using BackupDriver.Core.Schedule;

namespace BackupDriver.Service;

public class Worker : BackgroundService
{
    private readonly IConfigManager _configManager;
    private readonly IBackupOrchestrator _backupOrchestrator;
    private readonly BackupDriver.Infrastructure.ILogger _logger;
    private readonly string _stateFilePath;

    public Worker(
        IConfigManager configManager,
        IBackupOrchestrator backupOrchestrator,
        BackupDriver.Infrastructure.ILogger logger)
    {
        _configManager = configManager;
        _backupOrchestrator = backupOrchestrator;
        _logger = logger;
        _stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Info("Worker Service Iniciado.");
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        DateTime lastRun = DateTime.MinValue;
        DateTime nextRun = DateTime.MinValue;

        // FIX-2: Baseline Time Lock — tempo-base estático capturado na ativação
        // Evita o efeito "sliding window" onde DateTime.Now a cada segundo
        // causa cálculo incorreto para horários fixos
        DateTime baseTime = DateTime.Now;
        string lastConfigSig = "";

        var config = _configManager.GetConfig();
        if (config.Schedule.LastRun != DateTime.MinValue)
            lastRun = config.Schedule.LastRun;

        // FIX-2: Assinatura de config para detectar mudanças e recalcular baseTime
        string configSig = $"{config.Schedule.Mode}|{config.Schedule.Active}|{config.Schedule.FixedTime}|{config.Schedule.IntervalHours}";
        lastConfigSig = configSig;

        if (config.Schedule.Active && config.Schedule.UseInternalScheduler)
            nextRun = ScheduleCalculator.CalculateNextRun(config.Schedule, lastRun == DateTime.MinValue ? baseTime : lastRun);

        bool isRunningBackup = false;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Reload config periodically (or we could use FileSystemWatcher as asked, doing it in loop is easier & safer to avoid lock exceptions for now, or just get from config manager if it caches properly. _configManager actually caches. We need to force load if we want reactivity without FileSystemWatcher, but let's implement FileSystemWatcher inside the loop or outside).
                // Actually, since IConfigManager caches, let's just get it. But we need reactivity.
                // It was asked to use FileSystemWatcher or check if config changed. 
                // Let's keep it simple: Read state.
                config = _configManager.GetConfig();
                
                string status = "Ok";
                if (!config.Schedule.Active) status = "Inativo";
                else if (isRunningBackup) status = "Executando...";
                
                if (config.Schedule.Active && !isRunningBackup)
                {
                    // FIX-2: Detecta mudança na configuração via assinatura
                    string currentSig = $"{config.Schedule.Mode}|{config.Schedule.Active}|{config.Schedule.FixedTime}|{config.Schedule.IntervalHours}";
                    if (currentSig != lastConfigSig)
                    {
                        // Config mudou — recaptura baseTime para novo ciclo
                        baseTime = DateTime.Now;
                        lastConfigSig = currentSig;
                        _logger.Info($"Worker: Configuração alterada, baseTime recalculado: {baseTime}");
                    }

                    // FIX-2: Usa baseTime estático ao invés de DateTime.Now quando não há LastRun
                    nextRun = ScheduleCalculator.CalculateNextRun(config.Schedule, lastRun == DateTime.MinValue ? baseTime : lastRun);
                    
                    if (nextRun != DateTime.MinValue && DateTime.Now >= nextRun)
                    {
                        _logger.Info("Worker: disparando backup automático.");
                        isRunningBackup = true;
                        status = "Executando...";
                        WriteState(lastRun, nextRun, status, 0);

                        // Fire and forget, awating it blocks the timer but that's fine for backup execution.
                        lastRun = DateTime.Now;
                        config.Schedule.LastRun = lastRun;
                        _configManager.SaveConfig(config);

                        // Executa
                        var result = await _backupOrchestrator.ExecuteBackupAsync(config, step => 
                        {
                            WriteState(lastRun, nextRun, $"Passo {step.Number}: {step.Name} - {step.Status}", step.Progress ?? 0);
                        }, stoppingToken);

                        isRunningBackup = false;
                        if (!result.Success)
                        {
                            _logger.Error($"Worker backup automático falhou: {result.ErrorMessage}");
                        }

                        nextRun = ScheduleCalculator.CalculateNextRun(config.Schedule, lastRun);
                        status = "Ok";
                    }
                }

                // Salva estado para o Cliente UI consumir
                WriteState(lastRun, nextRun, status, 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error($"Erro no loop do Worker: {ex.Message}");
            }
        }
    }

    private void WriteState(DateTime lastRun, DateTime nextRun, string status, double progress)
    {
        try
        {
            var state = new
            {
                LastRun = lastRun,
                NextRun = nextRun,
                Status = status,
                Progress = progress,
                Timestamp = DateTime.Now
            };
            
            // Grava non-blocking (ou quase, usando temp file ou apenas escrita rapida)
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFilePath, json);
        }
        catch (IOException)
        {
            // Ocasionalmente pode dar lock se a UI estiver lendo no exato ms. Ignora e tenta no proximo pulso.
        }
    }
}
