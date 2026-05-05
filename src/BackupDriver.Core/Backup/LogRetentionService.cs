using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Backup;

// v1.6: Interface para retenção automática de arquivos de log (ADR-04)
public interface ILogRetentionService
{
    /// <summary>
    /// Remove arquivos .jsonl mais antigos que retentionDays do diretório de logs.
    /// </summary>
    int ApplyRetention(string logDirectory, int retentionDays);
}

// v1.6: Serviço de expurgo automático de logs antigos
// Executado na inicialização do app e após cada backup bem-sucedido
public class LogRetentionService : ILogRetentionService
{
    private readonly ILogger _logger;

    public LogRetentionService(ILogger logger)
    {
        _logger = logger;
    }

    public int ApplyRetention(string logDirectory, int retentionDays)
    {
        if (!Directory.Exists(logDirectory))
        {
            _logger.Info($"Diretório de logs não encontrado: {logDirectory}");
            return 0;
        }

        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        var logFiles = Directory.GetFiles(logDirectory, "*.jsonl");
        int removedCount = 0;

        foreach (var file in logFiles)
        {
            try
            {
                var lastWrite = File.GetLastWriteTime(file);
                if (lastWrite < cutoffDate)
                {
                    File.Delete(file);
                    removedCount++;
                    _logger.Info($"Log expirado removido: {Path.GetFileName(file)} (última escrita: {lastWrite:dd/MM/yyyy})");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Falha ao remover log {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (removedCount > 0)
        {
            _logger.Ok($"Retenção de logs: {removedCount} arquivo(s) removido(s) (limite: {retentionDays} dias)");
        }

        return removedCount;
    }
}
