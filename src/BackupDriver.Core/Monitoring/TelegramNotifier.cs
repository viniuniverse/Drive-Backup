namespace BackupDriver.Core.Monitoring;

public interface ITelegramNotifier
{
    Task<bool> SendMessageAsync(Config.TelegramConfig config, string message);
    Task<bool> TestMessageAsync(Config.TelegramConfig config);
}

public class TelegramNotifier : ITelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly Infrastructure.ILogger _logger;

    public TelegramNotifier(HttpClient httpClient, Infrastructure.ILogger logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(Config.TelegramConfig config, string message)
    {
        if (!config.Enabled || string.IsNullOrEmpty(config.BotToken) || string.IsNullOrEmpty(config.ChatId))
            return false;

        try
        {
            var url = $"https://api.telegram.org/bot{config.BotToken}/sendMessage";
            var payload = new
            {
                chat_id = config.ChatId,
                text = message,
                parse_mode = "Markdown"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.Info("Mensagem Telegram enviada.");
                return true;
            }

            _logger.Warn($"Telegram falhou: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Telegram erro: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestMessageAsync(Config.TelegramConfig config)
    {
        return await SendMessageAsync(config, "🧪 *BackupDriver*: Mensagem de teste enviada com sucesso!");
    }
}
