using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackupDriver.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private Guid _lastStatusId;

    /// <summary>
    /// Define uma mensagem de status temporária que desaparece após o tempo especificado.
    /// </summary>
    protected async Task SetTransientStatusAsync(Action<string> setter, string message, int delayMs = 5000)
    {
        var currentId = Guid.NewGuid();
        _lastStatusId = currentId;

        setter(message);

        await Task.Delay(delayMs);

        // Somente limpa se não houver uma mensagem mais nova agendada
        if (_lastStatusId == currentId)
        {
            setter(string.Empty);
        }
    }
}
