using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using BackupDriver.Core.Config;
using BackupDriver.Core.Services;
using BackupDriver.Infrastructure;
using BackupDriver.App.Messages;

namespace BackupDriver.App.ViewModels;

public partial class ConfigViewModel : ViewModelBase
{
    private readonly IConfigManager _configManager;
    private readonly ISecureStorage _secureStorage;
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger _logger;
    private readonly IDialogService _dialogService;
    private readonly IFolderPickerService _folderPickerService;

    [ObservableProperty]
    private string _firebirdPath = string.Empty;

    [ObservableProperty]
    private string _dbPath = string.Empty;

    [ObservableProperty]
    private string _user = "SYSDBA";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private int _port = 3050; // New v1.6.6

    [ObservableProperty]
    private string _backupOutputPath = string.Empty;

    [ObservableProperty]
    private string _zipPassword = string.Empty;

    [ObservableProperty]
    private int _retentionHours = 72;

    [ObservableProperty]
    private string _clientName = string.Empty;

    // v1.6.1: ADR-H04 — Retenção de logs
    [ObservableProperty]
    private int _logRetentionDays = 30;

    [ObservableProperty]
    private string _connectionTestResult = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ConfigViewModel(
        IConfigManager configManager,
        ISecureStorage secureStorage,
        IFileSystemService fileSystem,
        ILogger logger,
        IDialogService dialogService,
        IFolderPickerService folderPickerService)
    {
        _configManager = configManager;
        _secureStorage = secureStorage;
        _fileSystem = fileSystem;
        _logger = logger;
        _dialogService = dialogService;
        _folderPickerService = folderPickerService;
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configManager.GetConfig();
        FirebirdPath = config.Firebird.InstallPath;
        DbPath = config.Firebird.DbPath;
        User = config.Firebird.User;
        Password = config.Firebird.Password;
        Port = config.Firebird.Port;
        BackupOutputPath = config.Backup.OutputPath;
        ZipPassword = config.Backup.ZipPassword;
        RetentionHours = config.Backup.RetentionHours;
        ClientName = config.Rclone.ClientName;
        // v1.6.1: ADR-H04 — Carregar retenção de logs
        LogRetentionDays = config.Log.RetentionDays;
    }

    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken token)
    {
        if (string.IsNullOrEmpty(FirebirdPath) || string.IsNullOrEmpty(DbPath))
        {
            await _dialogService.ShowMessageAsync("Campos obrigatórios", "Preencha os campos obrigatórios.", DialogType.Error);
            return;
        }

        try
        {
            IsTesting = true;
            ConnectionTestResult = "Testando conexão...";

            var gbakPath = System.IO.Path.Combine(FirebirdPath, "gbak.exe");
            if (!_fileSystem.FileExists(gbakPath))
            {
                ConnectionTestResult = "❌ gbak.exe não encontrado no caminho informado.";
                await _dialogService.ShowMessageAsync("Falha na Conexão", "gbak.exe não encontrado no caminho informado.", DialogType.Error);
                return;
            }

            if (!_fileSystem.FileExists(DbPath))
            {
                ConnectionTestResult = "❌ Arquivo .FDB não encontrado.";
                await _dialogService.ShowMessageAsync("Falha na Conexão", "Arquivo .FDB não encontrado.", DialogType.Error);
                return;
            }

            var processRunner = new ProcessRunner();
            var result = await processRunner.RunAsync(
                gbakPath,
                $"-b -user {User} -pass {Password} \"localhost/{Port}:{DbPath}\" nul -v",
                new ProcessRunnerOptions { Timeout = TimeSpan.FromSeconds(30), CancellationToken = token }
            );

            if (result.Success)
            {
                _ = SetTransientStatusAsync(s => ConnectionTestResult = s, "✅ Conexão bem-sucedida!");
                _logger.Ok("Teste de conexão Firebird bem-sucedido.");
                await _dialogService.ShowMessageAsync("Conexão Bem-sucedida", "A conexão com o banco Firebird foi estabelecida com sucesso!", DialogType.Success);
            }
            else
            {
                _ = SetTransientStatusAsync(s => ConnectionTestResult = s, $"❌ Falha: {result.StandardError}");
                _logger.Error($"Teste de conexão falhou: {result.StandardError}");
                await _dialogService.ShowMessageAsync("Falha na Conexão", $"Não foi possível conectar ao banco Firebird.\n\nDetalhe: {result.StandardError}", DialogType.Error);
            }
        }
        catch (OperationCanceledException)
        {
            ConnectionTestResult = "❌ Operação cancelada.";
        }
        catch (Exception ex)
        {
            // T1: Fixar Falhas - Notificar timeout ou erro desconhecido
            ConnectionTestResult = "❌ Falha ao conectar: Tempo limite excedido ou erro desconhecido.";
            _logger.Error($"Exceção no teste de conexão: {ex.Message}");
            await _dialogService.ShowMessageAsync("Falha na Conexão", $"Erro inesperado: {ex.Message}", DialogType.Error);
        }
        finally
        {
            // T1: Estabilizar - Centralizar reset do estado de teste
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        var errors = new System.Collections.Generic.List<string>();

        if (string.IsNullOrEmpty(FirebirdPath))
            errors.Add("• Caminho do Firebird (gbak.exe)");
        if (string.IsNullOrEmpty(DbPath))
            errors.Add("• Caminho do banco (.FDB)");
        if (string.IsNullOrEmpty(BackupOutputPath))
            errors.Add("• Pasta de saída dos backups");

        if (errors.Count > 0)
        {
            var errorList = string.Join("\n", errors);
            await _dialogService.ShowMessageAsync("Campos inválidos", $"Os seguintes campos precisam ser corrigidos:\n\n{errorList}", DialogType.Error);
            return;
        }

        if (!_fileSystem.FileExists(DbPath))
        {
            await _dialogService.ShowMessageAsync("Erro de validação", "Arquivo .FDB não existe no caminho informado.", DialogType.Error);
            return;
        }

        var gbakPath = System.IO.Path.Combine(FirebirdPath, "gbak.exe");
        if (!_fileSystem.FileExists(gbakPath))
        {
            await _dialogService.ShowMessageAsync("Erro de validação", "gbak.exe não encontrado no caminho informado.", DialogType.Error);
            return;
        }

        _fileSystem.EnsureDirectoryExists(BackupOutputPath);

        var config = _configManager.GetConfig();
        config.Firebird.InstallPath = FirebirdPath;
        config.Firebird.DbPath = DbPath;
        config.Firebird.User = User;
        config.Firebird.Password = Password;
        config.Firebird.Port = Port;
        config.Backup.OutputPath = BackupOutputPath;
        config.Backup.ZipPassword = ZipPassword;
        config.Backup.RetentionHours = RetentionHours;
        config.Rclone.ClientName = ClientName;
        // v1.6.1: ADR-H04 — Persistir retenção de logs
        config.Log.RetentionDays = LogRetentionDays;

        _configManager.SaveConfig(config);
        _logger.Ok("Configuração salva com sucesso.");
        _ = SetTransientStatusAsync(s => StatusMessage = s, "✅ Configuração salva!");

        // T3.1: Notifica Dashboard de mudança de configuração
        WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());

        await _dialogService.ShowMessageAsync("Configuração Salva", "Suas configurações foram salvas com sucesso!", DialogType.Success);
    }

    [RelayCommand]
    private void ClearConfig()
    {
        FirebirdPath = string.Empty;
        DbPath = string.Empty;
        User = "SYSDBA";
        Password = string.Empty;
        Port = 3050;
        BackupOutputPath = string.Empty;
        ZipPassword = string.Empty;
        RetentionHours = 72;
        ClientName = string.Empty;
        // v1.6.1: ADR-H04 — Limpar retenção de logs
        LogRetentionDays = 30;
        ConnectionTestResult = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task BrowseFirebirdPathAsync()
    {
        var result = await _folderPickerService.PickFolderAsync("Selecionar pasta do Firebird");
        if (result != null)
            FirebirdPath = result;
    }

    [RelayCommand]
    private async Task BrowseDbPathAsync()
    {
        var result = await _folderPickerService.PickFileAsync("Selecionar banco Firebird", new[] { "*.FDB" });
        if (result != null)
            DbPath = result;
    }

    [RelayCommand]
    private async Task BrowseBackupPathAsync()
    {
        var result = await _folderPickerService.PickFolderAsync("Selecionar pasta de backup");
        if (result != null)
            BackupOutputPath = result;
    }
}