using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using BackupDriver.Core.Cloud;
using BackupDriver.Core.Config;
using BackupDriver.Infrastructure;
using BackupDriver.App.Messages;

namespace BackupDriver.App.ViewModels;

public partial class RcloneConfigViewModel : ViewModelBase
{
    private readonly IRcloneConfigurator _configurator;
    private readonly IRcloneValidator _validator;
    private readonly IConfigManager _configManager;
    private readonly ILogger _logger;
    private bool _isUpdatingFromConfig;

    [ObservableProperty]
    private string _remoteRoot = "BackupsFirebird";

    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    private bool _isConfigured;

    // v1.6: T-1.6.23 — Horas de retenção na nuvem (configurável na UI)
    [ObservableProperty]
    private decimal _cloudRetentionHours = 168;

    private bool _googleDriveEnabled = true;
    public bool GoogleDriveEnabled
    {
        get => _googleDriveEnabled;
        set
        {
            if (SetProperty(ref _googleDriveEnabled, value) && !_isUpdatingFromConfig)
            {
                SaveGoogleDriveEnabledAsync(value);
            }
        }
    }

    [ObservableProperty]
    private string _statusIcon = "🔴";

    [ObservableProperty]
    private string _statusText = "Não configurado";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConfiguring;

    [ObservableProperty]
    private bool _isTesting;

    public RcloneConfigViewModel(IRcloneConfigurator configurator, IRcloneValidator validator, IConfigManager configManager, ILogger logger)
    {
        _configurator = configurator;
        _validator = validator;
        _configManager = configManager;
        _logger = logger;
        LoadConfig();
    }

    private void LoadConfig()
    {
        _isUpdatingFromConfig = true;
        var config = _configManager.GetConfig();
        RemoteRoot = config.Rclone.RemoteRoot;
        ClientName = config.Rclone.ClientName;
        IsConfigured = config.Rclone.Configured;
        // v1.6: Carregar retenção cloud
        CloudRetentionHours = config.Rclone.CloudRetentionHours;
        _googleDriveEnabled = config.Rclone.Enabled;
        OnPropertyChanged(nameof(GoogleDriveEnabled));
        _isUpdatingFromConfig = false;
        UpdateStatus();
    }

    private void SaveGoogleDriveEnabledAsync(bool isEnabled)
    {
        var config = _configManager.GetConfig();
        config.Rclone.Enabled = isEnabled;
        _configManager.SaveConfig(config);
        
        var msg = isEnabled ? "Google Drive ativado!" : "Google Drive desativado.";
        _ = SetTransientStatusAsync(s => StatusMessage = s, msg);
        
        WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
    }

    private void UpdateStatus()
    {
        IsConfigured = _configurator.IsConfigured();
        StatusIcon = IsConfigured ? "🟢" : "🔴";
        StatusText = IsConfigured ? "Configurado" : "Não configurado";
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            var config = _configManager.GetConfig();
            config.Rclone.RemoteRoot = RemoteRoot;
            // v1.6: Persistir retenção cloud
            config.Rclone.CloudRetentionHours = (int)CloudRetentionHours;
            _configManager.SaveConfig(config);
            _ = SetTransientStatusAsync(s => StatusMessage = s, "Configuração salva!");

            WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
        }
        catch (System.IO.IOException ex)
        {
            _logger?.Error($"Erro ao salvar configuração: {ex.Message}");
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro ao salvar: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error($"Acesso negado ao salvar configuração: {ex.Message}");
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"Acesso negado: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Erro inesperado ao salvar configuração: {ex.Message}");
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ConfigureAsync(CancellationToken token)
    {
        IsConfiguring = true;
        StatusMessage = "Iniciando configuração OAuth...";

        var result = await _configurator.ConfigureAsync(progress =>
        {
            StatusMessage = progress;
        }, token);

        if (result.Success)
        {
            IsConfigured = true;
            var config = _configManager.GetConfig();
            config.Rclone.Configured = true;
            config.Rclone.RemoteRoot = RemoteRoot;
            config.Rclone.ClientName = ClientName;
            config.Rclone.Enabled = GoogleDriveEnabled;

            try
            {
                _configManager.SaveConfig(config);
                _ = SetTransientStatusAsync(s => StatusMessage = s, "Google Drive configurado com sucesso!");
                WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
            }
            catch (System.IO.IOException ex)
            {
                _logger?.Error($"Erro ao salvar configuração do Google Drive: {ex.Message}");
                _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro ao salvar: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.Error($"Acesso negado ao salvar configuração: {ex.Message}");
                _ = SetTransientStatusAsync(s => StatusMessage = s, $"Acesso negado: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Erro inesperado: {ex.Message}");
                _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro: {ex.Message}");
            }
        }
        else
        {
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"Falha: {result.ErrorMessage}");
        }

        UpdateStatus();
        IsConfiguring = false;
    }

    [RelayCommand]
    private async Task TestUploadAsync(CancellationToken token)
    {
        IsTesting = true;
        StatusMessage = "Testando upload...";

        var result = await _validator.TestUploadAsync(token);

        var msg = result.Success
            ? result.Message
            : result.Message;

        _ = SetTransientStatusAsync(s => StatusMessage = s, msg);

        IsTesting = false;
    }

    [RelayCommand]
    private async Task UnbindAsync(CancellationToken token)
    {
        StatusMessage = "Desvinculando conta...";

        try
        {
            await _configurator.UnlinkAsync();

            IsConfigured = false;
            var config = _configManager.GetConfig();
            config.Rclone.Configured = false;

            try
            {
                _configManager.SaveConfig(config);
                UpdateStatus();
                _ = SetTransientStatusAsync(s => StatusMessage = s, "Conta desvinculada com sucesso!");
                WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
            }
            catch (System.IO.IOException ex)
            {
                _logger?.Error($"Erro ao salvar após desvincular: {ex.Message}");
                _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro ao salvar: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.Error($"Acesso negado: {ex.Message}");
                _ = SetTransientStatusAsync(s => StatusMessage = s, $"Acesso negado: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Erro inesperado: {ex.Message}");
                _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _ = SetTransientStatusAsync(s => StatusMessage = s, $"Erro ao desvincular: {ex.Message}");
        }
    }
}