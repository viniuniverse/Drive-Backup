using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BackupDriver.App.Messages;

/// <summary>
/// Mensagem enviada para notificar mudança de estado em tempo real (Habilitado/Desabilitado)
/// de um módulo específico antes mesmo de salvar no disco.
/// </summary>
/// <param name="ModuleName">Nome do módulo (ex: "Healthcheck", "Telegram", "GoogleDrive")</param>
/// <param name="IsEnabled">Novo estado de ativação</param>
public record ModuleStatusChangedMessage(string ModuleName, bool IsEnabled);
