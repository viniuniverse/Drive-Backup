using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BackupDriver.App.Messages;

/// <summary>
/// Mensagem disparada pelo StartupUpdateCheckService quando uma versão
/// mais nova é encontrada no GitHub Releases.
/// O MainWindowViewModel escuta esta mensagem para exibir o dialog de atualização.
/// </summary>
public class UpdateAvailableMessage : ValueChangedMessage<(string NewVersion, string ReleaseUrl)>
{
    public UpdateAvailableMessage(string newVersion, string releaseUrl)
        : base((newVersion, releaseUrl)) { }

    /// <summary>Versão nova encontrada no GitHub, ex: "v1.7.0"</summary>
    public string NewVersion => Value.NewVersion;

    /// <summary>URL da página de release no GitHub para abrir no browser</summary>
    public string ReleaseUrl => Value.ReleaseUrl;
}
