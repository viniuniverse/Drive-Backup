using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Schedule;

/// <summary>
/// LEGADO: implementação via Windows Task Scheduler (schtasks.exe).
/// 
/// US-A5 (Task 5.12): Este serviço foi removido do fluxo principal.
/// O agendamento agora é feito pelo InternalSchedulerService com PeriodicTimer.
/// 
/// O código abaixo está MANTIDO como fallback/referência histórica.
/// Para reativar, registre esta classe no DI em App.axaml.cs.
/// </summary>
public class TaskSchedulerService : ISchedulerService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;
    private const string TASK_NAME = "BackupDriver_AutoBackup";

    public TaskSchedulerService(IProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// LEGADO: Criava tarefas no Windows Task Scheduler via schtasks.exe.
    /// Substituído pelo InternalSchedulerService (PeriodicTimer interno).
    /// </summary>
    public async Task<bool> ScheduleAsync(ScheduleSettings settings, string appPath)
    {
        _logger.Info("[LEGADO] TaskSchedulerService.ScheduleAsync chamado — sem efeito (substituído pelo InternalSchedulerService).");

        // ── CÓDIGO LEGADO — NÃO REMOVER (mantido como referência) ──
        // await RemoveScheduleAsync();
        //
        // if (!settings.Active)
        // {
        //     _logger.Info("Agendamento desativado.");
        //     return true;
        // }
        //
        // string scheduleArg;
        // if (settings.Mode == ScheduleMode.Interval)
        // {
        //     scheduleArg = $"/SC HOURLY /MO {settings.IntervalHours}";
        // }
        // else
        // {
        //     scheduleArg = $"/SC DAILY /ST {settings.FixedTime}";
        // }
        //
        // var args = $"/Create /TN {TASK_NAME} /TR \"\\\"{appPath}\\\" backup\" {scheduleArg} /RL HIGHEST /F";
        //
        // var result = await _processRunner.RunAsync(
        //     "schtasks",
        //     args,
        //     new ProcessRunnerOptions { Timeout = TimeSpan.FromSeconds(30) }
        // );
        //
        // if (result.Success)
        // {
        //     _logger.Ok($"Tarefa agendada: {TASK_NAME} ({settings.Mode})");
        //     return true;
        // }
        //
        // _logger.Error($"Falha ao criar tarefa: {result.StandardError}");
        // return false;

        return await Task.FromResult(true);
    }

    /// <summary>
    /// LEGADO: Removia tarefa do Windows Task Scheduler.
    /// </summary>
    public async Task<bool> RemoveScheduleAsync()
    {
        _logger.Info("[LEGADO] TaskSchedulerService.RemoveScheduleAsync chamado — sem efeito.");

        // ── CÓDIGO LEGADO ──
        // var result = await _processRunner.RunAsync(
        //     "schtasks",
        //     $"/Delete /TN {TASK_NAME} /F",
        //     new ProcessRunnerOptions { Timeout = TimeSpan.FromSeconds(30) }
        // );
        // return result.Success;

        return await Task.FromResult(true);
    }

    /// <summary>
    /// LEGADO: Consultava próxima execução via schtasks /Query.
    /// </summary>
    public async Task<DateTime?> GetNextRunTimeAsync()
    {
        _logger.Info("[LEGADO] TaskSchedulerService.GetNextRunTimeAsync chamado — retornando null.");

        // ── CÓDIGO LEGADO ──
        // var result = await _processRunner.RunAsync(
        //     "schtasks",
        //     $"/Query /TN {TASK_NAME} /V /FO CSV",
        //     new ProcessRunnerOptions { Timeout = TimeSpan.FromSeconds(10) }
        // );
        //
        // if (!result.Success) return null;
        //
        // foreach (var line in result.StandardOutput.Split('\n'))
        // {
        //     if (line.Contains("Next Run Time"))
        //     {
        //         var parts = line.Split(',');
        //         if (parts.Length >= 2)
        //         {
        //             var nextRunStr = parts.Last().Trim().Trim('"');
        //             if (DateTime.TryParse(nextRunStr, out var nextRun))
        //                 return nextRun;
        //         }
        //     }
        // }

        return await Task.FromResult<DateTime?>(null);
    }

    /// <summary>
    /// LEGADO: Verificava se a tarefa existia no Task Scheduler.
    /// </summary>
    public bool IsScheduled()
    {
        _logger.Info("[LEGADO] TaskSchedulerService.IsScheduled chamado — retornando false.");

        // ── CÓDIGO LEGADO ──
        // var result = _processRunner.RunAsync(
        //     "schtasks",
        //     $"/Query /TN {TASK_NAME}",
        //     new ProcessRunnerOptions { Timeout = TimeSpan.FromSeconds(10) }
        // ).GetAwaiter().GetResult();
        // return result.Success;

        return false;
    }
}
