using BackupDriver.Infrastructure;
using ICSharpCode.SharpZipLib.Zip;

namespace BackupDriver.Core.Backup;

public class CompressionResult
{
    public bool Success { get; set; }
    public string OutputFilePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
}

public interface ICompressionService
{
    Task<CompressionResult> CompressAsync(string inputPath, string outputPath, string? password = null, Action<string>? onProgress = null, CancellationToken ct = default);
}

// v1.6: Compressão nativa via SharpZipLib — substitui 7z.exe externo (ADR-05)
// Formato: .zip com AES-256. Zero dependência de executável externo.
public class CompressionService : ICompressionService
{
    private readonly IFileSystemService _fileSystem;
    private readonly Infrastructure.ILogger _logger;

    // v1.6: Construtor simplificado — sem IProcessRunner (não precisa mais de processo externo)
    public CompressionService(IFileSystemService fileSystem, Infrastructure.ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<CompressionResult> CompressAsync(string inputPath, string outputPath, string? password = null, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.Info($"Comprimindo: {inputPath}");
            onProgress?.Invoke("Comprimindo com ZIP (AES-256)...");

            // v1.6: Compressão nativa usando SharpZipLib com AES-256
            await Task.Run(() =>
            {
                using var outputStream = File.Create(outputPath);
                using var zipStream = new ZipOutputStream(outputStream);

                // Configurar senha AES-256 se fornecida
                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                }

                // Nível máximo de compressão
                zipStream.SetLevel(9);

                var fileInfo = new FileInfo(inputPath);
                var entryName = Path.GetFileName(inputPath);
                var entry = new ZipEntry(entryName)
                {
                    DateTime = fileInfo.LastWriteTime,
                    Size = fileInfo.Length
                };

                // v1.6: Usar AES-256 em vez de ZipCrypto para segurança real
                if (!string.IsNullOrEmpty(password))
                {
                    entry.AESKeySize = 256;
                }

                zipStream.PutNextEntry(entry);

                // Copiar arquivo para o ZIP com progresso
                using var inputStream = File.OpenRead(inputPath);
                var buffer = new byte[81920]; // 80KB buffer
                long totalRead = 0;
                int bytesRead;
                int lastPercent = 0;

                while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    zipStream.Write(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    // Reportar progresso a cada 5%
                    var percent = (int)((double)totalRead / fileInfo.Length * 100);
                    if (percent >= lastPercent + 5)
                    {
                        lastPercent = percent;
                        onProgress?.Invoke($"Comprimindo... {percent}%");
                    }
                }

                zipStream.CloseEntry();
                zipStream.Finish();
            }, ct);

            stopwatch.Stop();

            if (_fileSystem.FileExists(outputPath))
            {
                var fileSize = _fileSystem.GetFileSize(outputPath);
                _logger.Ok($"Compressão concluída: {outputPath} ({fileSize} bytes)");
                return new CompressionResult
                {
                    Success = true,
                    OutputFilePath = outputPath,
                    FileSizeBytes = fileSize,
                    Duration = stopwatch.Elapsed
                };
            }

            _logger.Error("Compressão falhou: arquivo de saída não encontrado.");
            return new CompressionResult
            {
                ErrorMessage = "Arquivo de saída não encontrado.",
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("Compressão cancelada pelo usuário.");
            // Limpar arquivo parcial
            if (_fileSystem.FileExists(outputPath))
                _fileSystem.DeleteFile(outputPath);

            return new CompressionResult
            {
                ErrorMessage = "Operação cancelada.",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Compressão falhou: {ex.Message}");
            return new CompressionResult
            {
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }
}

