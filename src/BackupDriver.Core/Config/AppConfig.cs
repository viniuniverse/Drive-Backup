namespace BackupDriver.Core.Config;

public class AppConfig
{
    public FirebirdConfig Firebird { get; set; } = new();
    public BackupConfig Backup { get; set; } = new();
    public RcloneConfig Rclone { get; set; } = new();
    public HealthcheckConfig Healthcheck { get; set; } = new();
    public ScheduleConfig Schedule { get; set; } = new();
    public TelegramConfig Telegram { get; set; } = new();
    public RestoreConfig Restore { get; set; } = new();
    // v1.6: Configuração de retenção de logs
    public LogConfig Log { get; set; } = new();
}

public class RestoreConfig
{
    public string GbakPath { get; set; } = string.Empty;
    public string DefaultTargetFolder { get; set; } = string.Empty;
}

public class FirebirdConfig
{
    public string InstallPath { get; set; } = string.Empty;
    public string DbPath { get; set; } = string.Empty;
    public string User { get; set; } = "SYSDBA";
    public string Password { get; set; } = string.Empty;
}

public class BackupConfig
{
    public string OutputPath { get; set; } = string.Empty;
    public string ZipPassword { get; set; } = string.Empty;
    public int RetentionHours { get; set; } = 72;
}

public class RcloneConfig
{
    public string RemoteName { get; set; } = "gdrive";
    public string RemoteRoot { get; set; } = "BackupsFirebird";
    public string ClientName { get; set; } = string.Empty;
    public bool Configured { get; set; }
    // v1.6.1: ADR-H05 — Padrão alterado para false (usuário deve ativar explicitamente)
    public bool Enabled { get; set; }
    // v1.6: Retenção automática de backups na nuvem (padrão: 7 dias = 168h)
    public int CloudRetentionHours { get; set; } = 168;
    // v1.6: Data da última subida bem-sucedida para o Google Drive
    public DateTime LastCloudUploadDate { get; set; } = DateTime.MinValue;
}

public class HealthcheckConfig
{
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class ScheduleConfig
{
    public string Mode { get; set; } = "interval";
    public int IntervalHours { get; set; } = 4;
    public string FixedTime { get; set; } = "02:00";
    public bool Active { get; set; }
    public bool UseInternalScheduler { get; set; } = true;
    public DateTime LastRun { get; set; } = DateTime.MinValue;
}

public class TelegramConfig
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

// v1.6: Configuração de retenção de logs (.jsonl)
public class LogConfig
{
    /// <summary>
    /// Dias para manter arquivos de log. Logs mais antigos serão removidos automaticamente.
    /// Padrão: 30 dias.
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}

