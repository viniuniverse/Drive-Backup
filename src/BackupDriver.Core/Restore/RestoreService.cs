using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Restore;

public class RestoreResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

public interface IRestoreService
{
    Task<RestoreResult> RestoreAsync(string backupFilePath, string targetDirectory, string firebirdPath, string user, string password, string? zipPassword = null, Action<string>? onProgress = null, CancellationToken ct = default);
}

public class RestoreService : IRestoreService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;
    private readonly Infrastructure.ILogger _logger;
    private readonly string _appDirectory;

    public RestoreService(IProcessRunner processRunner, IFileSystemService fileSystem, Infrastructure.ILogger logger, string appDirectory)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _logger = logger;
        _appDirectory = appDirectory;
    }

    public async Task<RestoreResult> RestoreAsync(string backupFilePath, string targetDirectory, string firebirdPath, string user, string password, string? zipPassword = null, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var gbakPath = Path.Combine(firebirdPath, "gbak.exe");
        if (!_fileSystem.FileExists(gbakPath))
        {
            // Fallback para tentar encontrar na raiz se não estiver na pasta informada
            if (_fileSystem.FileExists(Path.Combine(firebirdPath, "bin", "gbak.exe")))
                gbakPath = Path.Combine(firebirdPath, "bin", "gbak.exe");
            else
                return new RestoreResult { ErrorMessage = $"gbak.exe não encontrado em {firebirdPath}" };
        }

        string fbkFile = backupFilePath;
        var tempDir = Path.Combine(Path.GetTempPath(), "BackupDriver_Restore");

        try
        {
            if (backupFilePath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            {
                onProgress?.Invoke("Descomprimindo arquivo...");
                _fileSystem.EnsureDirectoryExists(tempDir);

                var sevenZipPath = Path.Combine(_appDirectory, "tools", "7z", "7z.exe");
                if (!_fileSystem.FileExists(sevenZipPath))
                    sevenZipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");

                var passwordArg = !string.IsNullOrEmpty(zipPassword) ? $"-p{zipPassword}" : "";
                var extractResult = await _processRunner.RunAsync(
                    sevenZipPath,
                    $"x {passwordArg} \"{backupFilePath}\" -o\"{tempDir}\" -y",
                    new ProcessRunnerOptions { Timeout = TimeSpan.FromMinutes(5), CancellationToken = ct }
                );

                if (!extractResult.Success)
                    return new RestoreResult { ErrorMessage = $"Falha ao descomprimir: {extractResult.StandardError}" };

                var extractedFbk = Directory.GetFiles(tempDir, "*.FBK").FirstOrDefault()
                    ?? Directory.GetFiles(tempDir, "*.fbk").FirstOrDefault();

                if (extractedFbk is null)
                    return new RestoreResult { ErrorMessage = "Nenhum arquivo .FBK encontrado no arquivo .7z" };

                fbkFile = extractedFbk;
            }

            _fileSystem.EnsureDirectoryExists(targetDirectory);

            // Define o nome do banco baseado no backup original
            var baseName = Path.GetFileNameWithoutExtension(backupFilePath).Replace(".FBK", "", StringComparison.OrdinalIgnoreCase);
            var targetDbPath = Path.Combine(targetDirectory, baseName + ".FDB");

            onProgress?.Invoke("Restaurando banco...");
            _logger.Info($"Restore: {fbkFile} → {targetDbPath}");

            // [TASK 2] Bypass de DB Lock: adicionar localhost: para paths locais
            var normalizedTarget = targetDbPath;
            if (!targetDbPath.Contains(':') || (targetDbPath.StartsWith("C", StringComparison.OrdinalIgnoreCase) || targetDbPath.StartsWith("D", StringComparison.OrdinalIgnoreCase) || targetDbPath.Length >= 2 && targetDbPath[1] == ':'))
            {
                // Verifica se já tem prefixo de host
                if (!targetDbPath.Contains("localhost") && !targetDbPath.Contains("127.0.0.1") && !targetDbPath.StartsWith("//"))
                {
                    normalizedTarget = "localhost:" + targetDbPath;
                    _logger.Info($"Aplicando bypass localhost: {normalizedTarget}");
                }
            }

            // [TASK 2] Wrap de senha com aspas para evitar problemas com espaços
            var wrappedPassword = string.IsNullOrEmpty(password) ? "" : $"-pass \"{password}\"";
            var result = await _processRunner.RunAsync(
                gbakPath,
                $"-R -user {user} {wrappedPassword} \"{fbkFile}\" \"{normalizedTarget}\"",
                new ProcessRunnerOptions
                {
                    Timeout = TimeSpan.FromMinutes(10),
                    OnOutputLine = line => onProgress?.Invoke(line),
                    CancellationToken = ct
                }
            );

            if (result.Success)
            {
                _logger.Ok($"Restore concluído em {result.Duration.TotalSeconds:F0}s: {targetDbPath}");
                return new RestoreResult { Success = true, Duration = result.Duration };
            }

            _logger.Error($"Restore falhou: {result.StandardError}");
            return new RestoreResult { ErrorMessage = result.StandardError, Duration = result.Duration };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
