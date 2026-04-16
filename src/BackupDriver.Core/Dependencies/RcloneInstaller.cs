using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Dependencies;

public class RcloneInstaller : IDependencyInstaller
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;

    public string DependencyName => "rclone";

    public RcloneInstaller(IProcessRunner processRunner, IFileSystemService fileSystem)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
    }

    public bool IsInstalled(string appDirectory)
    {
        var localPath = Path.Combine(appDirectory, "tools", "rclone", "rclone.exe");
        return _fileSystem.FileExists(localPath);
    }

    public async Task<bool> InstallAsync(string appDirectory, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var toolsDir = Path.Combine(appDirectory, "tools", "rclone");
        _fileSystem.EnsureDirectoryExists(toolsDir);

        var zipPath = Path.Combine(toolsDir, "rclone.zip");
        var downloadUrl = "https://downloads.rclone.org/rclone-current-windows-amd64.zip";

        onProgress?.Invoke("Baixando rclone...");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        try
        {
            var response = await httpClient.GetAsync(downloadUrl, ct);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(zipPath, FileMode.Create);
            await response.Content.CopyToAsync(fs, ct);

            onProgress?.Invoke("Extraindo rclone...");

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, toolsDir, overwriteFiles: true);

            var extractedDir = Directory.GetDirectories(toolsDir, "rclone-*").FirstOrDefault();
            if (extractedDir is not null)
            {
                var exePath = Path.Combine(extractedDir, "rclone.exe");
                if (_fileSystem.FileExists(exePath))
                {
                    _fileSystem.CopyFile(exePath, Path.Combine(toolsDir, "rclone.exe"), overwrite: true);
                }
            }

            _fileSystem.DeleteFile(zipPath);

            onProgress?.Invoke("[OK] rclone instalado.");
            return true;
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Erro: {ex.Message}");
            return false;
        }
    }
}
