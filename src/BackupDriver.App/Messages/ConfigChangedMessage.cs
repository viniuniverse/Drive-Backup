using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BackupDriver.App.Messages;

/// <summary>
/// Mensagem enviada quando as configurações do Google Drive ou Healthcheck são alteradas.
/// T1.1: Mecanismo de Mensageria para sincronização em tempo real.
/// </summary>
public class ConfigChangedMessage : ValueChangedMessage<bool>
{
    public ConfigChangedMessage() : base(true) { }
}
