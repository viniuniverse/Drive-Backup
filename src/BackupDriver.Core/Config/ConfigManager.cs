using BackupDriver.Infrastructure;

namespace BackupDriver.Core.Config;

public interface IConfigManager
{
    AppConfig GetConfig();
    void SaveConfig(AppConfig config);
    bool ConfigExists();
}

// v1.6: ConfigManager com criptografia transparente de campos sensíveis (DPAPI)
// v1.6.2: ADR-B01 — Thread-safe com lock para evitar race conditions
public class ConfigManager : IConfigManager
{
    private readonly IConfigProvider _configProvider;
    private readonly ISecureStorage _secureStorage;
    private readonly Infrastructure.ILogger _logger;
    private readonly string _configPath;
    private readonly object _lock = new();
    private AppConfig? _cachedConfig;

    // v1.6: Construtor atualizado — recebe ISecureStorage para criptografia de senhas
    public ConfigManager(IConfigProvider configProvider, ISecureStorage secureStorage, Infrastructure.ILogger logger, string configPath)
    {
        _configProvider = configProvider;
        _secureStorage = secureStorage;
        _logger = logger;
        _configPath = configPath;
    }

    public AppConfig GetConfig()
    {
        lock (_lock)
        {
            // _cachedConfig removido para forçar recarregamento do arquivo e permitir sincronia entre a UI e o Serviço Worker
            try 
            {
                var config = _configProvider.Load<AppConfig>(_configPath) ?? new AppConfig();
                
                // v1.6: Descriptografar campos sensíveis na leitura
                bool needsReEncrypt = DecryptSensitiveFields(config);
                
                // v1.6: Migração automática — se detectou texto puro, re-salva criptografado
                if (needsReEncrypt)
                {
                    SaveConfig(config);
                }
                
                return config;
            }
            catch 
            {
                return _cachedConfig ?? new AppConfig();
            }
        }
    }

    public void SaveConfig(AppConfig config)
    {
        lock (_lock)
        {
            try
            {
                // v1.6: Clonar config para não alterar o objeto em memória ao criptografar
                var configToSave = CloneForSave(config);
                
                // v1.6: Criptografar campos sensíveis antes de persistir
                EncryptSensitiveFields(configToSave);
                
                _configProvider.Save(_configPath, configToSave);
                _cachedConfig = config;
            }
            catch (Exception ex)
            {
                _logger.Error($"Falha critica ao salvar configuracao (I/O): {ex.Message}");
            }
            
            // v1.6: Marcar config.json como arquivo oculto no Windows
            try
            {
                if (File.Exists(_configPath))
                {
                    File.SetAttributes(_configPath, File.GetAttributes(_configPath) | FileAttributes.Hidden);
                }
            }
            catch { /* Falha ao ocultar não é crítica */ }
        }
    }

    public bool ConfigExists() => _configProvider.Exists(_configPath);

    // v1.6: Descriptografa campos sensíveis. Retorna true se algum campo estava em texto puro (migração necessária)
    private bool DecryptSensitiveFields(AppConfig config)
    {
        bool needsMigration = false;

        // Firebird.Password
        if (!string.IsNullOrEmpty(config.Firebird.Password))
        {
            if (_secureStorage.IsEncrypted(config.Firebird.Password))
                config.Firebird.Password = _secureStorage.Decrypt(config.Firebird.Password);
            else
                needsMigration = true; // Texto puro detectado — precisa re-salvar criptografado
        }

        // Backup.ZipPassword
        if (!string.IsNullOrEmpty(config.Backup.ZipPassword))
        {
            if (_secureStorage.IsEncrypted(config.Backup.ZipPassword))
                config.Backup.ZipPassword = _secureStorage.Decrypt(config.Backup.ZipPassword);
            else
                needsMigration = true;
        }

        // Telegram.BotToken
        if (!string.IsNullOrEmpty(config.Telegram.BotToken))
        {
            if (_secureStorage.IsEncrypted(config.Telegram.BotToken))
                config.Telegram.BotToken = _secureStorage.Decrypt(config.Telegram.BotToken);
            else
                needsMigration = true;
        }

        return needsMigration;
    }

    // v1.6: Criptografa campos sensíveis antes de salvar no disco
    private void EncryptSensitiveFields(AppConfig config)
    {
        if (!string.IsNullOrEmpty(config.Firebird.Password))
            config.Firebird.Password = _secureStorage.Encrypt(config.Firebird.Password);

        if (!string.IsNullOrEmpty(config.Backup.ZipPassword))
            config.Backup.ZipPassword = _secureStorage.Encrypt(config.Backup.ZipPassword);

        if (!string.IsNullOrEmpty(config.Telegram.BotToken))
            config.Telegram.BotToken = _secureStorage.Encrypt(config.Telegram.BotToken);
    }

    // v1.6: Clone raso para não afetar o objeto em memória ao criptografar para salvar
    private static AppConfig CloneForSave(AppConfig original)
    {
        return new AppConfig
        {
            Firebird = new FirebirdConfig
            {
                InstallPath = original.Firebird.InstallPath,
                DbPath = original.Firebird.DbPath,
                User = original.Firebird.User,
                Password = original.Firebird.Password
            },
            Backup = new BackupConfig
            {
                OutputPath = original.Backup.OutputPath,
                ZipPassword = original.Backup.ZipPassword,
                RetentionHours = original.Backup.RetentionHours
            },
            Rclone = new RcloneConfig
            {
                RemoteName = original.Rclone.RemoteName,
                RemoteRoot = original.Rclone.RemoteRoot,
                ClientName = original.Rclone.ClientName,
                Configured = original.Rclone.Configured,
                Enabled = original.Rclone.Enabled,
                CloudRetentionHours = original.Rclone.CloudRetentionHours,
                LastCloudUploadDate = original.Rclone.LastCloudUploadDate
            },
            Healthcheck = new HealthcheckConfig
            {
                Url = original.Healthcheck.Url,
                Enabled = original.Healthcheck.Enabled
            },
            Schedule = new ScheduleConfig
            {
                Mode = original.Schedule.Mode,
                IntervalHours = original.Schedule.IntervalHours,
                FixedTime = original.Schedule.FixedTime,
                Active = original.Schedule.Active,
                UseInternalScheduler = original.Schedule.UseInternalScheduler,
                LastRun = original.Schedule.LastRun
            },
            Telegram = new TelegramConfig
            {
                BotToken = original.Telegram.BotToken,
                ChatId = original.Telegram.ChatId,
                Enabled = original.Telegram.Enabled
            },
            Restore = new RestoreConfig
            {
                GbakPath = original.Restore.GbakPath,
                DefaultTargetFolder = original.Restore.DefaultTargetFolder
            },
            Log = new LogConfig
            {
                RetentionDays = original.Log.RetentionDays
            }
        };
    }
}

