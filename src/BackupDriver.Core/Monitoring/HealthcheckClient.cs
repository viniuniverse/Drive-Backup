namespace BackupDriver.Core.Monitoring;

public interface IHealthcheckClient
{
    Task SendStartPingAsync(Config.HealthcheckConfig config);
    Task SendSuccessPingAsync(Config.HealthcheckConfig config);
    Task SendFailurePingAsync(Config.HealthcheckConfig config, int errorCode);
    Task<bool> TestPingAsync(Config.HealthcheckConfig config);
}

public class HealthcheckClient : IHealthcheckClient
{
    private readonly HttpClient _httpClient;
    private readonly Infrastructure.ILogger _logger;

    public HealthcheckClient(HttpClient httpClient, Infrastructure.ILogger logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    public async Task SendStartPingAsync(Config.HealthcheckConfig config)
    {
        if (!config.Enabled || string.IsNullOrEmpty(config.Url))
            return;

        try
        {
            var response = await _httpClient.GetAsync($"{config.Url}/start");
            _logger.Info($"Healthcheck /start: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Healthcheck /start falhou: {ex.Message}");
        }
    }

    public async Task SendSuccessPingAsync(Config.HealthcheckConfig config)
    {
        if (!config.Enabled || string.IsNullOrEmpty(config.Url))
            return;

        try
        {
            var response = await _httpClient.GetAsync(config.Url);
            _logger.Info($"Healthcheck sucesso: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Healthcheck sucesso falhou: {ex.Message}");
        }
    }

    public async Task SendFailurePingAsync(Config.HealthcheckConfig config, int errorCode)
    {
        if (!config.Enabled || string.IsNullOrEmpty(config.Url))
            return;

        try
        {
            var response = await _httpClient.GetAsync($"{config.Url}/fail");
            _logger.Info($"Healthcheck falha (código {errorCode}): {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Healthcheck falha falhou: {ex.Message}");
        }
    }

    public async Task<bool> TestPingAsync(Config.HealthcheckConfig config)
    {
        if (string.IsNullOrEmpty(config.Url))
            return false;

        try
        {
            var response = await _httpClient.GetAsync(config.Url);
            _logger.Info($"Healthcheck teste: {response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Error($"Healthcheck teste falhou: {ex.Message}");
            return false;
        }
    }
}
