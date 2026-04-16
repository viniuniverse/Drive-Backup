namespace BackupDriver.Core.Dependencies;

public interface IDependencyInstaller
{
    string DependencyName { get; }
    bool IsInstalled(string appDirectory);
    Task<bool> InstallAsync(string appDirectory, Action<string>? onProgress = null, CancellationToken ct = default);
}

public class DependencyStatus
{
    public string Name { get; set; } = string.Empty;
    public bool Installed { get; set; }
    public string? Version { get; set; }
}
