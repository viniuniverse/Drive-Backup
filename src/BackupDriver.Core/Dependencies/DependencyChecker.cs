namespace BackupDriver.Core.Dependencies;

public interface IDependencyChecker
{
    List<DependencyStatus> CheckAll(string appDirectory);
    Task<bool> InstallMissingAsync(string appDirectory, Action<string>? onProgress = null, CancellationToken ct = default);
}

public class DependencyChecker : IDependencyChecker
{
    private readonly List<IDependencyInstaller> _installers;

    public DependencyChecker(IEnumerable<IDependencyInstaller> installers)
    {
        _installers = installers.ToList();
    }

    public List<DependencyStatus> CheckAll(string appDirectory)
    {
        return _installers.Select(i => new DependencyStatus
        {
            Name = i.DependencyName,
            Installed = i.IsInstalled(appDirectory)
        }).ToList();
    }

    public async Task<bool> InstallMissingAsync(string appDirectory, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        foreach (var installer in _installers)
        {
            if (!installer.IsInstalled(appDirectory))
            {
                onProgress?.Invoke($"Instalando {installer.DependencyName}...");
                var success = await installer.InstallAsync(appDirectory, onProgress, ct);
                if (!success)
                    return false;
            }
            else
            {
                onProgress?.Invoke($"[OK] {installer.DependencyName} já instalado.");
            }
        }
        return true;
    }
}
