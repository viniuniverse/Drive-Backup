using BackupDriver.Service;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net.Http;
using System;
using BackupDriver.Core.Backup;
using BackupDriver.Core.Cloud;
using BackupDriver.Core.Config;
using BackupDriver.Core.Monitoring;
using BackupDriver.Infrastructure;

// 1. Encontrar o diretório base (pode ser o do executável ou onde o config está)
var appDir = AppDomain.CurrentDomain.BaseDirectory;
// Assumindo que a UI e o Serviço compartilham o mesmo config na pasta pai ou appdir? 
// Como eles compartilham, vamos apontar pro mesmo config. Temporariamente usando baseDir.
// Normalmente o serviço instala na mesma pasta da UI, então BaseDirectory será o mesmo.
var configPath = Path.Combine(appDir, "config.json");
var logDir = Path.Combine(appDir, "logs_worker");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "BackupDriver Service";
});

// Configure Services
var services = builder.Services;

services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<IFileSystemService, FileSystemService>();
services.AddSingleton<BackupDriver.Infrastructure.ILogger>(new Logger(logDir));
services.AddSingleton<IConfigProvider, JsonConfigProvider>();
services.AddSingleton<ISecureStorage, SecureStorage>();
services.AddSingleton<IConfigManager>(sp =>
    new ConfigManager(sp.GetRequiredService<IConfigProvider>(), configPath));

// Backup Executors
services.AddSingleton<IGbakExecutor, GbakExecutor>();
services.AddSingleton<ICompressionService>(sp =>
    new CompressionService(
        sp.GetRequiredService<IProcessRunner>(),
        sp.GetRequiredService<IFileSystemService>(),
        sp.GetRequiredService<BackupDriver.Infrastructure.ILogger>(),
        appDir));

// Cloud and Monitoring
services.AddSingleton<HttpClient>();
services.AddSingleton<IHealthcheckClient, HealthcheckClient>();
services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
services.AddSingleton<IRcloneExecutor, RcloneExecutor>();
services.AddSingleton<IRcloneConfigurator, RcloneConfigurator>();
services.AddSingleton<IRcloneValidator, RcloneValidator>();

// Orchestrator
services.AddSingleton<IBackupOrchestrator>(sp =>
    new BackupOrchestrator(
        sp.GetRequiredService<IGbakExecutor>(),
        sp.GetRequiredService<ICompressionService>(),
        sp.GetRequiredService<IRcloneExecutor>(),
        sp.GetRequiredService<IHealthcheckClient>(),
        sp.GetRequiredService<IFileSystemService>(),
        sp.GetRequiredService<BackupDriver.Infrastructure.ILogger>(),
        appDir));

// Finally register the Worker itself
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
