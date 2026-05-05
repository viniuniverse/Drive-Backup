using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using BackupDriver.App.ViewModels;
using BackupDriver.App.Views;
using Microsoft.Extensions.DependencyInjection;
using BackupDriver.Core.Backup;
using BackupDriver.Core.Cloud;
using BackupDriver.Core.Config;
using BackupDriver.Core.Dependencies;
using BackupDriver.Core.Monitoring;
using BackupDriver.Core.Restore;
using BackupDriver.Core.Schedule;
using BackupDriver.Infrastructure;
using BackupDriver.Core.Services;
using BackupDriver.App.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace BackupDriver.App;

public partial class App : Application
{
    // Flag para permitir fechamento real (ao sair pelo menu)
    private bool _isExiting;
    // Mantém referência ao scheduler interno para controle de ciclo de vida
    private InternalSchedulerService? _internalScheduler;
    // v1.6: TrayIconManager unificado (ADR-03) — singleton gerenciado via DI
    private TrayIconManager? _trayIconManager;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            DisableAvaloniaDataAnnotationValidation();

            var services = ConfigureServices();
            var mainWindowViewModel = services.GetRequiredService<MainWindowViewModel>();

            // v1.7.0: Injeta referência circular resolvida após DI estar pronto (ADR-005 / T4)
            // Permite que ConfigViewModel propague SetProState() ao ativar uma licença
            services.GetRequiredService<ConfigViewModel>().SetMainWindowViewModel(mainWindowViewModel);

            var mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel,
            };
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            // v1.6: TrayIconManager unificado — remove duplicação (ADR-03 / T-1.6.28)
            // Toda lógica de TrayIcon concentrada no TrayIconManager
            _trayIconManager = services.GetRequiredService<TrayIconManager>();
            _trayIconManager.Initialize(mainWindow);
            _trayIconManager.OnExitRequested = () =>
            {
                _isExiting = true;
                _internalScheduler?.Stop();
                _trayIconManager?.Dispose();
                desktop.Shutdown();
            };

            // v1.6: Registrar tray no Avalonia para ficar sempre visível
            TrayIcon.GetIcons(this)?.Clear();

            // v1.6: Retenção de logs na inicialização (ADR-04)
            try
            {
                var logRetention = services.GetRequiredService<ILogRetentionService>();
                var configManager = services.GetRequiredService<IConfigManager>();
                var config = configManager.GetConfig();
                var logDir = Path.Combine(appDir, "logs");
                logRetention.ApplyRetention(logDir, config.Log.RetentionDays);
            }
            catch { /* Falha na retenção de logs na inicialização não é crítica */ }

            // Obtém referências dos serviços já registrados
            _internalScheduler = services.GetRequiredService<InternalSchedulerService>();
            var configMgr = services.GetRequiredService<IConfigManager>();

            // v1.3.2: Motor unificado — InternalSchedulerService executa backup diretamente
            // Inicia o agendador interno automaticamente (motor completo de scheduling + execução)
            var cfgStartup = configMgr.GetConfig();
            if (cfgStartup.Schedule.UseInternalScheduler)
            {
                _internalScheduler.Start();
            }

            // v1.6.6: Verificação de atualização assíncrona (ADR-UP-02)
            // Fire-and-forget: não bloqueia a abertura da janela.
            // Se offline ou falhar, opera silenciosamente sem afetar o usuário.
            var updateCheckService = services.GetRequiredService<StartupUpdateCheckService>();
            _ = updateCheckService.CheckAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(appDir, "config.json");
        var logDir = Path.Combine(appDir, "logs");

        // ── Infraestrutura base ──
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<ILogger>(new Logger(logDir));
        services.AddSingleton<IConfigProvider, JsonConfigProvider>();
        services.AddSingleton<IRcloneBootstrapService, RcloneBootstrapService>();

        // v1.6: SecureStorage + ConfigManager com criptografia DPAPI (ADR-06 / T-1.6.03)
        services.AddSingleton<ISecureStorage, SecureStorage>();
        services.AddSingleton<IConfigManager>(sp =>
            new ConfigManager(
                sp.GetRequiredService<IConfigProvider>(),
                sp.GetRequiredService<ISecureStorage>(),
                sp.GetRequiredService<ILogger>(),
                configPath));

        // v1.7.0: Licenciamento PRO — Feature Toggles (ADR-005 / T1-T2)
        // Validator usa chave pública RSA embutida; Storage usa DPAPI do Windows
        services.AddSingleton<ILicenseValidatorService, LicenseValidatorService>();
        services.AddSingleton<ILicenseStorage, LicenseStorage>();

        // ── Dependências externas ──
        // v1.6: SevenZipInstaller removido do DI (T-1.6.12) — substituído por SharpZipLib
        services.AddSingleton<IDependencyInstaller, RcloneInstaller>();
        services.AddSingleton<IDependencyChecker, DependencyChecker>();

        // ── Executores de backup ──
        services.AddSingleton<IGbakExecutor, GbakExecutor>();
        // v1.6: CompressionService simplificado — sem IProcessRunner (T-1.6.10)
        services.AddSingleton<ICompressionService>(sp =>
            new CompressionService(
                sp.GetRequiredService<IFileSystemService>(),
                sp.GetRequiredService<ILogger>()));

        // ── Retenção ──
        services.AddSingleton<IRetentionPolicy>(sp =>
            new RetentionPolicy(
                sp.GetRequiredService<IFileSystemService>(),
                sp.GetRequiredService<ILogger>()));
        // v1.6: Retenção de logs (ADR-04 / T-1.6.23)
        services.AddSingleton<ILogRetentionService>(sp =>
            new LogRetentionService(sp.GetRequiredService<ILogger>()));

        // ── Monitoramento e cloud ──
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IHealthcheckClient, HealthcheckClient>();
        services.AddSingleton<ITelegramNotifier, TelegramNotifier>();

        services.AddSingleton<IRcloneExecutor, RcloneExecutor>();
        services.AddSingleton<IRcloneConfigurator, RcloneConfigurator>();
        services.AddSingleton<IRcloneValidator, RcloneValidator>();

        // ── Orquestrador principal de backup ──
        // v1.6: Atualizado com ILogRetentionService e IConfigManager (T-1.6.22)
        services.AddSingleton<IBackupOrchestrator>(sp =>
            new BackupOrchestrator(
                sp.GetRequiredService<IGbakExecutor>(),
                sp.GetRequiredService<ICompressionService>(),
                sp.GetRequiredService<IRcloneExecutor>(),
                sp.GetRequiredService<IHealthcheckClient>(),
                sp.GetRequiredService<IFileSystemService>(),
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IRetentionPolicy>(),
                sp.GetRequiredService<ILogRetentionService>(),
                sp.GetRequiredService<IConfigManager>(),
                appDir));

        // ── v1.3.2: Motor unificado — InternalSchedulerService com IBackupOrchestrator ──
        services.AddSingleton<InternalSchedulerService>(sp =>
            new InternalSchedulerService(
                sp.GetRequiredService<IConfigManager>(),
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IBackupOrchestrator>()));
        services.AddSingleton<ISchedulerService>(sp => sp.GetRequiredService<InternalSchedulerService>());

        // ── US-A1 + US-A2: Serviços de UI desacoplados ──
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();

        // v1.6: TrayIconManager como Singleton (ADR-03 / T-1.6.28)
        services.AddSingleton<TrayIconManager>();

        // v1.6.6: Serviço de verificação de atualização na inicialização (ADR-UP-01)
        // Reutiliza o HttpClient já registrado no DI — sem dependência nova
        services.AddSingleton<StartupUpdateCheckService>();

        // ── Restauração ──
        services.AddSingleton<IRestoreService>(sp =>
            new RestoreService(
                sp.GetRequiredService<IProcessRunner>(),
                sp.GetRequiredService<IFileSystemService>(),
                sp.GetRequiredService<ILogger>(),
                appDir));

        // ── ViewModels ──
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ConfigViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<RestoreViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<RcloneConfigViewModel>();
        services.AddSingleton<HealthcheckViewModel>();
        // v1.6.6: IDialogService injetado automaticamente pelo DI no MainWindowViewModel
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
