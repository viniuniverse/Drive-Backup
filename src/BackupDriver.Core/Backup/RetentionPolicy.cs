using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Backup;

public class RetentionResult
{
    public int RemovedCount { get; set; }
    public long FreedBytes { get; set; }
    public List<string> RemovedFiles { get; set; } = new();
}

public interface IRetentionPolicy
{
    RetentionResult Apply(string outputPath, int retentionHours);
}

public class RetentionPolicy : IRetentionPolicy
{
    private readonly IFileSystemService _fileSystem;
    private readonly Infrastructure.ILogger _logger;

    public RetentionPolicy(IFileSystemService fileSystem, Infrastructure.ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public RetentionResult Apply(string outputPath, int retentionHours)
    {
        var result = new RetentionResult();
        var now = DateTime.Now;
        var cutoff = now.AddHours(-retentionHours);

        _logger.Info($"Iniciando retenção em {outputPath} (Corte: {cutoff:yyyy-MM-dd HH:mm:ss}, Limite: {retentionHours}h)");

        // Busca tanto arquivos .zip (v1.6+), .7z (legados) quanto .fbk que possam ter sobrado de falhas
        var patterns = new[] { "*.zip", "*.7z", "*.FBK" };
        var files = new List<string>();
        foreach (var p in patterns)
        {
            // v1.6.4: Organização de pastas - busca recursiva
            files.AddRange(_fileSystem.GetFiles(outputPath, p, recursive: true));
        }

        foreach (var file in files)
        {
            var lastWrite = _fileSystem.GetLastWriteTime(file);
            var age = now - lastWrite;

            if (lastWrite < cutoff)
            {
                var size = _fileSystem.GetFileSize(file);
                _logger.Info($"Removendo backup antigo ({age.TotalHours:F1}h de idade): {Path.GetFileName(file)}");

                try
                {
                    _fileSystem.DeleteFile(file);
                    result.RemovedCount++;
                    result.FreedBytes += size;
                    result.RemovedFiles.Add(file);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Falha ao remover {file}: {ex.Message}");
                }
            }
        }

        if (result.RemovedCount > 0)
        {
            _logger.Info($"Retenção: {result.RemovedCount} arquivos removidos ({result.FreedBytes} bytes liberados)");
            // v1.6.4: Limpa diretórios vazios resultantes da retenção
            _fileSystem.DeleteEmptyDirectories(outputPath);
        }

        return result;
    }
}
