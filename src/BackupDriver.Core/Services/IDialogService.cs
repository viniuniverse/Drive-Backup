using System.Threading.Tasks;

namespace BackupDriver.Core.Services;

public enum DialogType
{
    Success,
    Error,
    Info,
    Warning
}

public interface IDialogService
{
    /// <summary>Exibe dialog informativo com botão OK.</summary>
    Task ShowMessageAsync(string title, string message, DialogType type = DialogType.Info);

    /// <summary>
    /// Exibe dialog de atualização disponível com dois botões:
    /// "Baixar agora" (abre browser no releaseUrl) e "Agora não" (fecha).
    /// </summary>
    Task ShowUpdateDialogAsync(string newVersion, string releaseUrl);
}