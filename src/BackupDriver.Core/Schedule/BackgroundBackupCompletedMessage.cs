using CommunityToolkit.Mvvm.Messaging.Messages;
using BackupDriver.Core.Backup;

namespace BackupDriver.Core.Schedule;

/// <summary>
/// Mensagem enviada quando um backup automático (background) é concluído pelo agendador interno.
/// v1.3.2: Movida para Core.Schedule para eliminar dependência circular App → Core.
/// Permite que o DashboardViewModel atualize os campos de "Último Backup" em tempo real.
/// </summary>
public class BackgroundBackupCompletedMessage : ValueChangedMessage<BackupResult>
{
    public BackgroundBackupCompletedMessage(BackupResult result) : base(result) { }
}
