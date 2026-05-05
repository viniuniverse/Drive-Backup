using BackupDriver.Core.Config;

namespace BackupDriver.Core.Backup;

public class BackupStep
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Aguardando...";
    public double? Progress { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
}

public class BackupResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
}

public interface IBackupOrchestrator
{
    Task<BackupResult> ExecuteBackupAsync(AppConfig config, Action<BackupStep>? onStepUpdate = null, CancellationToken ct = default);
}

public class BackupOrchestrator : IBackupOrchestrator
{
    private readonly IGbakExecutor _gbakExecutor;
    private readonly ICompressionService _compressionService;
    private readonly Cloud.IRcloneExecutor _rcloneExecutor;
    private readonly Monitoring.IHealthcheckClient _healthcheckClient;
    private readonly Infrastructure.IFileSystemService _fileSystem;
    private readonly Infrastructure.ILogger _logger;
    private readonly IRetentionPolicy _retentionPolicy;
    // v1.6: Novos serviços de retenção
    private readonly ILogRetentionService _logRetentionService;
    private readonly Config.IConfigManager _configManager;
    private readonly string _appDirectory;

    public BackupOrchestrator(
        IGbakExecutor gbakExecutor,
        ICompressionService compressionService,
        Cloud.IRcloneExecutor rcloneExecutor,
        Monitoring.IHealthcheckClient healthcheckClient,
        Infrastructure.IFileSystemService fileSystem,
        Infrastructure.ILogger logger,
        IRetentionPolicy retentionPolicy,
        ILogRetentionService logRetentionService,
        Config.IConfigManager configManager,
        string appDirectory)
    {
        _gbakExecutor = gbakExecutor;
        _compressionService = compressionService;
        _rcloneExecutor = rcloneExecutor;
        _healthcheckClient = healthcheckClient;
        _fileSystem = fileSystem;
        _logger = logger;
        _retentionPolicy = retentionPolicy;
        _logRetentionService = logRetentionService;
        _configManager = configManager;
        _appDirectory = appDirectory;
    }

    public async Task<BackupResult> ExecuteBackupAsync(AppConfig config, Action<BackupStep>? onStepUpdate = null, CancellationToken ct = default)
    {
        var steps = new[]
        {
            new BackupStep { Number = 1, Name = "Backup Firebird" },
            new BackupStep { Number = 2, Name = "Comprimir (ZIP)" },
            new BackupStep { Number = 3, Name = "Upload Drive" },
            new BackupStep { Number = 4, Name = "Healthcheck" }
        };

        void UpdateStep(int index, string status, bool completed = false, bool failed = false, double? progress = null)
        {
            steps[index].Status = status;
            steps[index].IsCompleted = completed;
            steps[index].IsFailed = failed;
            steps[index].Progress = progress;
            onStepUpdate?.Invoke(steps[index]);
        }

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        await _healthcheckClient.SendStartPingAsync(config.Healthcheck);

        string fbkFile = string.Empty;

        try
        {
            UpdateStep(0, "Executando...");
            var gbakResult = await _gbakExecutor.ExecuteAsync(
                config.Firebird.DbPath,
                config.Firebird.Port,
                config.Firebird.User,
                config.Firebird.Password,
                config.Firebird.InstallPath,
                config.Backup.OutputPath,
                progress => UpdateStep(0, progress),
                ct
            );

            if (!gbakResult.Success)
            {
                UpdateStep(0, "Falhou", failed: true);
                await _healthcheckClient.SendFailurePingAsync(config.Healthcheck, 1);
                return new BackupResult { ErrorMessage = $"GBAK falhou: {gbakResult.ErrorMessage}" };
            }

            fbkFile = gbakResult.OutputFilePath;
            UpdateStep(0, $"Concluído — {gbakResult.Duration.TotalSeconds:F0}s", completed: true);

            UpdateStep(1, "Executando...");
            var now = DateTime.Now;
            var timestamp = now.ToString("yyyyMMdd_HHmmss");
            
            // [TASK 3] Nome do backup baseado no Nome do Cliente + data/hora
            var clientNamePrefix = config.Rclone.ClientName;
            if (string.IsNullOrWhiteSpace(clientNamePrefix))
            {
                clientNamePrefix = Path.GetFileNameWithoutExtension(fbkFile);
            }
            else
            {
                // Remove caracteres inválidos para nome de arquivo
                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    clientNamePrefix = clientNamePrefix.Replace(c, '_');
                }
            }

            // v1.6.4: Organização de pastas - Local Path
            var dateSubPath = now.ToString("yyyy/MM/dd").Replace('/', Path.DirectorySeparatorChar);
            var localTargetDir = Path.Combine(config.Backup.OutputPath, dateSubPath);
            _fileSystem.EnsureDirectoryExists(localTargetDir);

            // v1.6: Formato .zip em vez de .7z (SharpZipLib + AES-256)
            var zipFile = Path.Combine(localTargetDir, $"{clientNamePrefix}_{timestamp}.zip");

            var compressionResult = await _compressionService.CompressAsync(
                fbkFile,
                zipFile,
                config.Backup.ZipPassword,
                progress => UpdateStep(1, progress),
                ct
            );

            if (!compressionResult.Success)
            {
                UpdateStep(1, "Falhou", failed: true);
                await _healthcheckClient.SendFailurePingAsync(config.Healthcheck, 2);
                return new BackupResult { ErrorMessage = $"Compressão falhou: {compressionResult.ErrorMessage}" };
            }

            UpdateStep(1, $"Concluído — {compressionResult.Duration.TotalSeconds:F0}s", completed: true);

            if (!config.Rclone.Enabled)
            {
                _logger.Info("Upload para Google Drive pulado — integração desabilitada.");
                UpdateStep(2, "PULADO (OFF)", completed: true);
            }
            else
            {
                UpdateStep(2, "Executando...");
                // v1.6.4: Organização de pastas - Cloud Path
                var dateSubPathCloud = now.ToString("yyyy/MM/dd");
                var remotePath = $"{config.Rclone.RemoteRoot}/{config.Rclone.ClientName}/{dateSubPathCloud}";

                var uploadResult = await _rcloneExecutor.UploadAsync(
                    zipFile,
                    remotePath,
                    progress => UpdateStep(2, progress),
                    ct
                );

                if (!uploadResult.Success)
                {
                    UpdateStep(2, "Falhou", failed: true);
                    await _healthcheckClient.SendFailurePingAsync(config.Healthcheck, 3);
                    return new BackupResult { ErrorMessage = $"Upload falhou: {uploadResult.ErrorMessage}" };
                }

                UpdateStep(2, $"Concluído — {uploadResult.Duration.TotalSeconds:F0}s", completed: true);

                // v1.6: Persistir data da última subida bem-sucedida
                config.Rclone.LastCloudUploadDate = DateTime.Now;
                _configManager.SaveConfig(config);

                // v1.6: Retenção automática na nuvem (ADR-04)
                if (config.Rclone.CloudRetentionHours > 0)
                {
                    var cloudRetentionPath = $"{config.Rclone.RemoteRoot}/{config.Rclone.ClientName}";
                    await _rcloneExecutor.ApplyCloudRetentionAsync(cloudRetentionPath, config.Rclone.CloudRetentionHours, ct);
                }
            }

            if (config.Healthcheck.Enabled)
            {
                UpdateStep(3, "Enviando ping...");
                await _healthcheckClient.SendSuccessPingAsync(config.Healthcheck);
                UpdateStep(3, "Concluído", completed: true);
            }
            else
            {
                UpdateStep(3, "PULADO (OFF)", completed: true);
            }

            _fileSystem.DeleteFile(fbkFile);

            _retentionPolicy.Apply(config.Backup.OutputPath, config.Backup.RetentionHours);

            // v1.6: Retenção automática de logs antigos (ADR-04)
            var logDir = Path.Combine(_appDirectory, "logs");
            _logRetentionService.ApplyRetention(logDir, config.Log.RetentionDays);

            totalStopwatch.Stop();

            _logger.Ok($"Backup completo: {zipFile} ({compressionResult.FileSizeBytes} bytes)");

            return new BackupResult
            {
                Success = true,
                OutputFile = zipFile,
                FileSizeBytes = compressionResult.FileSizeBytes,
                Duration = totalStopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Backup falhou: {ex.Message}");
            await _healthcheckClient.SendFailurePingAsync(config.Healthcheck, 99);

            if (!string.IsNullOrEmpty(fbkFile))
                _fileSystem.DeleteFile(fbkFile);

            return new BackupResult { ErrorMessage = ex.Message };
        }
    }
}
