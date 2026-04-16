using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Cloud;

public class ValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface IRcloneValidator
{
    Task<ValidationResult> TestUploadAsync(CancellationToken ct = default);
}

public class RcloneValidator : IRcloneValidator
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;
    private readonly IRcloneBootstrapService _bootstrapService;
    private readonly Infrastructure.ILogger _logger;

    public RcloneValidator(
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

    public async Task<ValidationResult> TestUploadAsync(CancellationToken ct = default)
    {
        var rclonePath = _bootstrapService.GetRclonePath();
        var configPath = GetConfigPath();

        if (!_fileSystem.FileExists(rclonePath))
            return new ValidationResult { Message = "rclone.exe não encontrado." };

        if (!_fileSystem.FileExists(configPath))
            return new ValidationResult { Message = "Google Drive não configurado." };

        var testFile = Path.Combine(Path.GetTempPath(), "backupdriver_test.txt");
        await File.WriteAllTextAsync(testFile, $"BackupDriver test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ct);

        try
        {
            var result = await _processRunner.RunAsync(
                rclonePath,
                $"copy \"{testFile}\" gdrive:BackupsFirebird/test/ --progress --config \"{configPath}\"",
                new ProcessRunnerOptions { Timeout = TimeSpan.FromSeconds(30), CancellationToken = ct }
            );

            _fileSystem.DeleteFile(testFile);

            if (result.Success)
            {
                _logger.Ok("Teste de upload rclone bem-sucedido.");
                return new ValidationResult { Success = true, Message = "Upload de teste bem-sucedido!" };
            }

            return new ValidationResult { Message = $"Falha no teste: {result.StandardError}" };
        }
        catch (Exception ex)
        {
            _fileSystem.DeleteFile(testFile);
            return new ValidationResult { Message = $"Erro: {ex.Message}" };
        }
    }

    private static string GetConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "BackupDriver", "rclone.conf");
    }
}