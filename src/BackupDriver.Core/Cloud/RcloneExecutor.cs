using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Cloud;

public class UploadResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

public interface IRcloneExecutor
{
    Task<UploadResult> UploadAsync(string localPath, string remotePath, Action<string>? onProgress = null, CancellationToken ct = default);
    string GetRclonePath();
    // v1.6: Retenção automática de backups antigos na nuvem (ADR-04)
    Task<bool> ApplyCloudRetentionAsync(string remotePath, int retentionHours, CancellationToken ct = default);
}

public class RcloneExecutor : IRcloneExecutor
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;
    private readonly IRcloneBootstrapService _bootstrapService;
    private readonly Infrastructure.ILogger _logger;
    private const int MAX_RETRIES = 3;

    public RcloneExecutor(
        IProcessRunner processRunner,
        IFileSystemService fileSystem,
        IRcloneBootstrapService bootstrapService,
        ILogger logger)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _bootstrapService = bootstrapService;
        _logger = logger;
    }

    public string GetRclonePath()
    {
        return _bootstrapService.GetRclonePath();
    }

    private static string GetConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "BackupDriver", "rclone.conf");
    }

    public async Task<UploadResult> UploadAsync(string localPath, string remotePath, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        await _bootstrapService.EnsureRcloneExtractedAsync();
        var rclonePath = _bootstrapService.GetRclonePath();
        var configPath = GetConfigPath();

        if (!_fileSystem.FileExists(rclonePath))
        {
            _logger.Error("rclone.exe não encontrado.");
            return new UploadResult { ErrorMessage = "rclone.exe não encontrado." };
        }

        _logger.Info($"Iniciando upload: {localPath} → {remotePath}");
        onProgress?.Invoke("Enviando para Google Drive...");

        for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
        {
            try
            {
                var result = await _processRunner.RunAsync(
                    rclonePath,
                    $"copy \"{localPath}\" gdrive:{remotePath} --progress --stats-one-line --config \"{configPath}\"",
                    new ProcessRunnerOptions
                    {
                        Timeout = TimeSpan.FromMinutes(10),
                        OnOutputLine = line => onProgress?.Invoke(line),
                        CancellationToken = ct
                    }
                );

                if (result.Success)
                {
                    _logger.Ok($"Upload concluído: {remotePath}");
                    return new UploadResult { Success = true, Duration = result.Duration };
                }

                if (attempt < MAX_RETRIES)
                {
                    var delay = (int)Math.Pow(2, attempt) * 1000;
                    _logger.Warn($"Upload falhou (tentativa {attempt}/{MAX_RETRIES}). Retry em {delay}ms...");
                    onProgress?.Invoke($"Retry {attempt}/{MAX_RETRIES}...");
                    await Task.Delay(delay, ct);
                }
                else
                {
                    _logger.Error($"Upload falhou após {MAX_RETRIES} tentativas: {result.StandardError}");
                    return new UploadResult { ErrorMessage = result.StandardError, Duration = result.Duration };
                }
            }
            catch (Exception ex) when (attempt < MAX_RETRIES)
            {
                var delay = (int)Math.Pow(2, attempt) * 1000;
                _logger.Warn($"Upload erro (tentativa {attempt}): {ex.Message}. Retry em {delay}ms...");
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                return new UploadResult { ErrorMessage = ex.Message };
            }
        }

        return new UploadResult { ErrorMessage = "Upload falhou após todas as tentativas." };
    }

    // v1.6: Retenção automática na nuvem — remove backups mais antigos que N horas (ADR-04)
    // Executa: rclone delete gdrive:{remotePath} --min-age {retentionHours}h --config "rclone.conf"
    public async Task<bool> ApplyCloudRetentionAsync(string remotePath, int retentionHours, CancellationToken ct = default)
    {
        try
        {
            await _bootstrapService.EnsureRcloneExtractedAsync();
            var rclonePath = _bootstrapService.GetRclonePath();
            var configPath = GetConfigPath();

            if (!_fileSystem.FileExists(rclonePath))
            {
                _logger.Error("rclone.exe não encontrado para retenção cloud.");
                return false;
            }

            _logger.Info($"Iniciando retenção na nuvem: {remotePath} (min-age: {retentionHours}h)");

            var result = await _processRunner.RunAsync(
                rclonePath,
                $"delete gdrive:{remotePath} --min-age {retentionHours}h --config \"{configPath}\"",
                new ProcessRunnerOptions
                {
                    Timeout = TimeSpan.FromMinutes(5),
                    CancellationToken = ct
                }
            );

            if (result.Success)
            {
                _logger.Ok($"Retenção cloud concluída: {remotePath}");
                return true;
            }

            _logger.Warn($"Retenção cloud falhou: {result.StandardError}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Erro na retenção cloud: {ex.Message}");
            return false;
        }
    }
}