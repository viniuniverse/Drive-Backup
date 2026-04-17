using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Cloud;

public class RcloneConfigureResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public interface IRcloneConfigurator
{
    Task<RcloneConfigureResult> ConfigureAsync(Action<string>? onProgress = null, CancellationToken ct = default);
    Task UnlinkAsync();
    bool IsConfigured();
}

public class RcloneConfigurator : IRcloneConfigurator
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;
    private readonly IRcloneBootstrapService _bootstrapService;
    private readonly Infrastructure.ILogger _logger;
    private const string RemoteName = "gdrive";

    public RcloneConfigurator(
        IProcessRunner processRunner,
        IFileSystemService fileSystem,
        IRcloneBootstrapService bootstrapService,
        Infrastructure.ILogger logger)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _bootstrapService = bootstrapService;
        _logger = logger;
    }

    public async Task<RcloneConfigureResult> ConfigureAsync(Action<string>? onProgress = null, CancellationToken ct = default)
    {
        KillStaleProcesses();

        await _bootstrapService.EnsureRcloneExtractedAsync();
        var rclonePath = _bootstrapService.GetRclonePath();
        var configPath = GetConfigPath();

        if (!_fileSystem.FileExists(rclonePath))
            return new RcloneConfigureResult { ErrorMessage = "rclone.exe não encontrado." };

        onProgress?.Invoke("Iniciando configuração OAuth do Google Drive...");

        var result = await _processRunner.RunAsync(
            rclonePath,
            $"config create {RemoteName} drive scope drive config_is_local true --config \"{configPath}\"",
            new ProcessRunnerOptions
            {
                Timeout = TimeSpan.FromMinutes(5),
                OnOutputLine = line => 
                {
                    onProgress?.Invoke(line);
                    
                    // Task 3: Isolamento Anônimo de Sessão
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"http://(127\.0\.0\.1|localhost):\d+(/[^\s]*)?");
                    if (match.Success)
                    {
                        var url = match.Value;
                        if (!TryLaunchIncognito(url))
                        {
                            // Fallback se nenhum anônimo funcionar
                            try 
                            { 
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); 
                            } 
                            catch { }
                        }
                    }
                },
                CancellationToken = ct
            }
        );

        if (result.Success)
        {
            _logger.Ok("rclone configurado com sucesso.");
            return new RcloneConfigureResult { Success = true };
        }

        _logger.Error($"Falha na configuração: {result.StandardError}");
        return new RcloneConfigureResult
        {
            Success = false,
            ErrorMessage = result.StandardError
        };
    }

    public async Task UnlinkAsync()
    {
        KillStaleProcesses();

        var configPath = GetConfigPath();
        if (_fileSystem.FileExists(configPath))
        {
            try
            {
                File.Delete(configPath);
                _logger.Ok("Arquivo de configuração rclone.conf removido com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Erro ao remover rclone.conf: {ex.Message}");
            }
        }
        
        await Task.CompletedTask;
    }

    public bool IsConfigured()
    {
        var configPath = GetConfigPath();
        if (!_fileSystem.FileExists(configPath))
            return false;

        try
        {
            var content = File.ReadAllText(configPath);
            return content.Contains($"[{RemoteName}]");
        }
        catch
        {
            return false;
        }
    }

    private static string GetConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "BackupDriver", "rclone.conf");
    }

    private void KillStaleProcesses()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("rclone");
            foreach (var process in processes)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    _logger.Ok($"Processo zumbi rclone (PID: {process.Id}) encerrado forçadamente.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Erro ao tentar encerrar processos zumbis do rclone: {ex.Message}");
        }
    }

    private bool TryLaunchIncognito(string url)
    {
        var browsers = new[]
        {
            new { Name = "chrome.exe", Args = "--incognito" },
            new { Name = "msedge.exe", Args = "-inprivate" },
            new { Name = "firefox.exe", Args = "-private-window" }
        };

        foreach (var browser in browsers)
        {
            try
            {
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = browser.Name,
                    Arguments = $"{browser.Args} \"{url}\"",
                    UseShellExecute = true
                });
                
                if (p != null)
                {
                    _logger.Ok($"Navegador {browser.Name} aberto em modo anônimo para OAuth.");
                    return true;
                }
            }
            catch { /* ignorar e tentar próximo navegador */ }
        }
        return false;
    }
}