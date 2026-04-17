using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Backup;

public class GbakResult
{
    public bool Success { get; set; }
    public string OutputFilePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

public interface IGbakExecutor
{
    Task<GbakResult> ExecuteAsync(string dbPath, int port, string user, string password, string firebirdPath, string outputDir, Action<string>? onProgress = null, CancellationToken ct = default);
}

public class GbakExecutor : IGbakExecutor
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;
    private readonly Infrastructure.ILogger _logger;

    public GbakExecutor(IProcessRunner processRunner, IFileSystemService fileSystem, Infrastructure.ILogger logger)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<GbakResult> ExecuteAsync(string dbPath, int port, string user, string password, string firebirdPath, string outputDir, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var gbakPath = Path.Combine(firebirdPath, "gbak.exe");

        if (!_fileSystem.FileExists(gbakPath))
        {
            _logger.Error($"GBAK não encontrado em: {gbakPath}");
            return new GbakResult { ErrorMessage = $"gbak.exe não encontrado em {gbakPath}" };
        }

        if (!_fileSystem.FileExists(dbPath))
        {
            _logger.Error($"Banco não encontrado: {dbPath}");
            return new GbakResult { ErrorMessage = $"Banco não encontrado: {dbPath}" };
        }

        _fileSystem.EnsureDirectoryExists(outputDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dbName = Path.GetFileNameWithoutExtension(dbPath);
        var fbkFile = Path.Combine(outputDir, $"{dbName}_{timestamp}.FBK");

        _logger.Info($"Iniciando backup: {dbPath}");

        onProgress?.Invoke("Executando gbak...");

        var result = await _processRunner.RunAsync(
            gbakPath,
            $"-b -user {user} -pass {password} \"localhost/{port}:{dbPath}\" \"{fbkFile}\"",
            new ProcessRunnerOptions
            {
                Timeout = TimeSpan.FromMinutes(10),
                OnOutputLine = line => onProgress?.Invoke(line),
                OnErrorLine = line => _logger.Warn($"GBAK stderr: {line}"),
                CancellationToken = ct
            }
        );

        if (result.Success && _fileSystem.FileExists(fbkFile))
        {
            _logger.Ok($"GBAK concluído: {fbkFile}");
            return new GbakResult
            {
                Success = true,
                OutputFilePath = fbkFile,
                Duration = result.Duration
            };
        }

        _logger.Error($"GBAK falhou: {result.StandardError}");
        return new GbakResult
        {
            ErrorMessage = result.StandardError,
            Duration = result.Duration
        };
    }
}
