using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Dependencies;

// v1.6: Substituído por SharpZipLib (compressão nativa). Mantido por retrocompatibilidade.
[Obsolete("v1.6: Substituído por SharpZipLib. Será removido em versão futura.")]
public class SevenZipInstaller : IDependencyInstaller
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;

    public string DependencyName => "7-Zip";

    public SevenZipInstaller(IProcessRunner processRunner, IFileSystemService fileSystem)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
    }

    public bool IsInstalled(string appDirectory)
    {
        var localPath = Path.Combine(appDirectory, "tools", "7z", "7z.exe");
        if (_fileSystem.FileExists(localPath))
            return true;

        var programFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
        return _fileSystem.FileExists(programFiles);
    }

    public async Task<bool> InstallAsync(string appDirectory, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var toolsDir = Path.Combine(appDirectory, "tools", "7z");
        _fileSystem.EnsureDirectoryExists(toolsDir);

        var installerPath = Path.Combine(toolsDir, "7z-installer.msi");
        var downloadUrl = "https://www.7-zip.org/a/7z2409-x64.msi";

        onProgress?.Invoke("Baixando 7-Zip...");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        try
        {
            var response = await httpClient.GetAsync(downloadUrl, ct);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(installerPath, FileMode.Create);
            await response.Content.CopyToAsync(fs, ct);

            onProgress?.Invoke("Instalando 7-Zip...");

            var result = await _processRunner.RunAsync(
                "msiexec",
                $"/i \"{installerPath}\" /quiet INSTALLDIR=\"{toolsDir}\"",
                new ProcessRunnerOptions { Timeout = TimeSpan.FromMinutes(3), CancellationToken = ct }
            );

            if (!result.Success)
            {
                onProgress?.Invoke("Instalação via MSI falhou. Extraindo portable...");
                await ExtractPortableAsync(installerPath, toolsDir, ct);
            }

            onProgress?.Invoke("[OK] 7-Zip instalado.");
            return true;
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"Erro: {ex.Message}");
            return false;
        }
    }

    private async Task ExtractPortableAsync(string msiPath, string targetDir, CancellationToken ct)
    {
        var result = await _processRunner.RunAsync(
            "msiexec",
            $"/a \"{msiPath}\" /qb TARGETDIR=\"{targetDir}\"",
            new ProcessRunnerOptions { Timeout = TimeSpan.FromMinutes(3), CancellationToken = ct }
        );

        if (!result.Success)
            throw new InvalidOperationException("Falha ao extrair 7-Zip portable.");
    }
}
