using System.Reflection;

namespace BackupDriver.Infrastructure;

public interface IRcloneBootstrapService
{
    string GetRclonePath();
    Task EnsureRcloneExtractedAsync();
}

public class RcloneBootstrapService : IRcloneBootstrapService
{
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger _logger;
    private static string? _cachedRclonePath;
    private const string AppName = "BackupDriver";
    private const string RcloneFileName = "rclone.exe";

    public RcloneBootstrapService(IFileSystemService fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string GetRclonePath()
    {
        if (_cachedRclonePath != null && _fileSystem.FileExists(_cachedRclonePath))
            return _cachedRclonePath;

        var toolsDir = GetToolsDirectory();
        var rclonePath = Path.Combine(toolsDir, RcloneFileName);
        _cachedRclonePath = rclonePath;
        return rclonePath;
    }

    public async Task EnsureRcloneExtractedAsync()
    {
        var rclonePath = GetRclonePath();

        if (_fileSystem.FileExists(rclonePath))
        {
            _logger.Info($"rclone.exe já existe em: {rclonePath}");
            return;
        }

        _logger.Info("Extraindo rclone.exe...");

        var toolsDir = GetToolsDirectory();
        _fileSystem.EnsureDirectoryExists(toolsDir);

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "rclone.exe";

        await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
        {
            _logger.Error("Recurso rclone.exe não encontrado no assembly.");
            throw new FileNotFoundException("Recurso rclone.exe não encontrado.");
        }

        await using var fileStream = new FileStream(rclonePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await resourceStream.CopyToAsync(fileStream);

        _logger.Ok($"rclone.exe extraído para: {rclonePath}");
    }

    private static string GetToolsDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppName, "tools");
    }
}