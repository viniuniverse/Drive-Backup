using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BackupDriver.Core.Config;
using BackupDriver.Core.Restore;
using BackupDriver.Core.Services;
using BackupDriver.Infrastructure;

namespace BackupDriver.App.ViewModels;

public partial class RestoreViewModel : ViewModelBase
{
    private readonly IRestoreService _restoreService;
    private readonly IConfigManager _configManager;
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger _logger;
    private readonly IFolderPickerService _folderPicker;

    [ObservableProperty]
    private string _backupFilePath = string.Empty;

    [ObservableProperty]
    private string _zipPassword = string.Empty;

    [ObservableProperty]
    private string _targetDirectory = string.Empty;

    [ObservableProperty]
    private string _gbakRestorePath = string.Empty;

    [ObservableProperty]
    private string _progressMessage = "Aguardando...";

    [ObservableProperty]
    private string _extractProgress = "Aguardando...";

    [ObservableProperty]
    private bool _isRestoring;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public RestoreViewModel(
        IRestoreService restoreService,
        IConfigManager configManager,
        IFileSystemService fileSystem,
        ILogger logger,
        IFolderPickerService folderPicker)
    {
        _restoreService = restoreService;
        _configManager = configManager;
        _fileSystem = fileSystem;
        _logger = logger;
        _folderPicker = folderPicker;
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configManager.GetConfig();
        GbakRestorePath = config.Restore.GbakPath;
        if (string.IsNullOrEmpty(GbakRestorePath))
            GbakRestorePath = config.Firebird.InstallPath;

        TargetDirectory = config.Restore.DefaultTargetFolder;
    }

    [RelayCommand]
    private async Task BrowseBackupFileAsync()
    {
        var file = await _folderPicker.PickFileAsync(
            "Selecionar arquivo de backup",
            new[] { "*.7z", "*.FBK", "*.fbk" }
        );

        if (!string.IsNullOrEmpty(file))
        {
            BackupFilePath = file;
        }
    }

    [RelayCommand]
    private async Task BrowseTargetFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync("Selecionar pasta de destino para o banco");
        if (!string.IsNullOrEmpty(folder))
        {
            TargetDirectory = folder;
        }
    }

    [RelayCommand]
    private async Task BrowseGbakPathAsync()
    {
        var folder = await _folderPicker.PickFolderAsync("Selecionar pasta contendo gbak.exe");
        if (!string.IsNullOrEmpty(folder))
        {
            GbakRestorePath = folder;
        }
    }

    [RelayCommand]
    private async Task StartRestoreAsync(CancellationToken token)
    {
        if (string.IsNullOrEmpty(BackupFilePath) || string.IsNullOrEmpty(TargetDirectory))
        {
            _ = SetTransientStatusAsync(s => StatusMessage = s, "❌ Preencha todos os campos obrigatórios.");
            return;
        }

        if (!_fileSystem.FileExists(BackupFilePath))
        {
            _ = SetTransientStatusAsync(s => StatusMessage = s, "❌ Arquivo de backup não encontrado.");
            return;
        }

        IsRestoring = true;
        ProgressMessage = "Aguardando...";
        ExtractProgress = "Aguardando...";

        var config = _configManager.GetConfig();
        
        // Salva caminhos usados para facilitar o próximo restore
        config.Restore.GbakPath = GbakRestorePath;
        config.Restore.DefaultTargetFolder = TargetDirectory;
        _configManager.SaveConfig(config);

        var result = await _restoreService.RestoreAsync(
            BackupFilePath,
            TargetDirectory,
            GbakRestorePath,
            config.Firebird.User,
            config.Firebird.Password,
            string.IsNullOrEmpty(ZipPassword) ? null : ZipPassword,
            progress =>
            {
                if (progress.Contains("Descomprimindo"))
                    ExtractProgress = progress;
                else
                    ProgressMessage = progress;
            },
            token
        );

        if (result.Success)
        {
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"✅ Restore concluído em {result.Duration.TotalSeconds:F0}s");
            _logger.Ok($"Restore concluído em: {TargetDirectory}");
        }
        else
        {
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"❌ Falha: {result.ErrorMessage}");
            _logger.Error($"Restore falhou: {result.ErrorMessage}");
        }

        IsRestoring = false;
    }
}
