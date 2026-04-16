using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using BackupDriver.Core.Config;
using BackupDriver.Core.Monitoring;
using BackupDriver.App.Messages;

namespace BackupDriver.App.ViewModels;

public partial class HealthcheckViewModel : ViewModelBase
{
    private readonly IHealthcheckClient _healthcheckClient;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly IConfigManager _configManager;

    [ObservableProperty]
    private bool _healthcheckEnabled;

    [ObservableProperty]
    private string _healthcheckUrl = string.Empty;

    [ObservableProperty]
    private string _lastHealthcheckResponse = "—";

    [ObservableProperty]
    private bool _telegramEnabled;

    [ObservableProperty]
    private string _telegramBotToken = string.Empty;

    [ObservableProperty]
    private string _telegramChatId = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    public HealthcheckViewModel(IHealthcheckClient healthcheckClient, ITelegramNotifier telegramNotifier, IConfigManager configManager)
    {
        _healthcheckClient = healthcheckClient;
        _telegramNotifier = telegramNotifier;
        _configManager = configManager;
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configManager.GetConfig();
        HealthcheckEnabled = config.Healthcheck.Enabled;
        HealthcheckUrl = config.Healthcheck.Url;
        TelegramEnabled = config.Telegram.Enabled;
        TelegramBotToken = config.Telegram.BotToken;
        TelegramChatId = config.Telegram.ChatId;
    }

    partial void OnHealthcheckEnabledChanged(bool value)
    {
        // v1.6.5: Sincronização em tempo real (Pre-Save)
        WeakReferenceMessenger.Default.Send(new ModuleStatusChangedMessage("Healthcheck", value));
    }

    partial void OnTelegramEnabledChanged(bool value)
    {
        // v1.6.5: Sincronização em tempo real (Pre-Save)
        WeakReferenceMessenger.Default.Send(new ModuleStatusChangedMessage("Telegram", value));
    }

    [RelayCommand]
    private async Task TestHealthcheckAsync(CancellationToken token)
    {
        IsTesting = true;
        StatusMessage = "Enviando ping de teste...";

        var config = new HealthcheckConfig
        {
            Enabled = true,
            Url = HealthcheckUrl
        };

        var success = await _healthcheckClient.TestPingAsync(config);
        LastHealthcheckResponse = success ? "HTTP 200 OK ✅" : "Falha ❌";
        _ = SetTransientStatusAsync(s => StatusMessage = s, success ? "✅ Healthcheck OK!" : "❌ Falha no healthcheck.");
        IsTesting = false;
    }

    [RelayCommand]
    private async Task TestTelegramAsync(CancellationToken token)
    {
        IsTesting = true;
        StatusMessage = "Enviando mensagem de teste...";

        var config = new TelegramConfig
        {
            Enabled = true,
            BotToken = TelegramBotToken,
            ChatId = TelegramChatId
        };

        var success = await _telegramNotifier.TestMessageAsync(config);
        _ = SetTransientStatusAsync(s => StatusMessage = s, success ? "✅ Mensagem enviada!" : "❌ Falha ao enviar mensagem.");
        IsTesting = false;
    }

    [RelayCommand]
    private void SaveConfig()
    {
        var config = _configManager.GetConfig();
        config.Healthcheck.Enabled = HealthcheckEnabled;
        config.Healthcheck.Url = HealthcheckUrl;
        config.Telegram.Enabled = TelegramEnabled;
        config.Telegram.BotToken = TelegramBotToken;
        config.Telegram.ChatId = TelegramChatId;

        _configManager.SaveConfig(config);

        // v1.6.5: T-1.6.50 — Notificar Dashboard e outros ouvintes de que a config mudou
        WeakReferenceMessenger.Default.Send(new ConfigChangedMessage());
        
        _ = SetTransientStatusAsync(s => StatusMessage = s, "✅ Configuração salva!");
    }
}
