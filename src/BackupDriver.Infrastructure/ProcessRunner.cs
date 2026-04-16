using System.Diagnostics;
using System.Text;

namespace BackupDriver.Infrastructure;

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success => ExitCode == 0;
    public TimeSpan Duration { get; set; }
}

public class ProcessRunnerOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
    public bool CaptureOutput { get; set; } = true;
    public string? WorkingDirectory { get; set; }
    public Action<string>? OnOutputLine { get; set; }
    public Action<string>? OnErrorLine { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string executablePath, string arguments, ProcessRunnerOptions? options = null);
}

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string executablePath, string arguments, ProcessRunnerOptions? options = null)
    {
        options ??= new ProcessRunnerOptions();
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = options.CaptureOutput,
            RedirectStandardError = options.CaptureOutput,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (options.WorkingDirectory is not null)
            process.StartInfo.WorkingDirectory = options.WorkingDirectory;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        if (options.CaptureOutput)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    outputBuilder.AppendLine(e.Data);
                    options.OnOutputLine?.Invoke(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    errorBuilder.AppendLine(e.Data);
                    options.OnErrorLine?.Invoke(e.Data);
                }
            };
        }

        process.Start();

        if (options.CaptureOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        cts.CancelAfter(options.Timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Process '{executablePath}' timed out after {options.Timeout.TotalSeconds} seconds.");
        }

        stopwatch.Stop();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuilder.ToString(),
            StandardError = errorBuilder.ToString(),
            Duration = stopwatch.Elapsed
        };
    }
}
