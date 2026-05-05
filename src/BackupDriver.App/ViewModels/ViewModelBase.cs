using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BackupDriver.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private Guid _lastStatusId;

    // ─── Feature Toggle PRO (ADR-005) ────────────────────────────────────────
    // Propriedade global para todas as Views usarem IsVisible="{Binding IsProVersion}"
    // Definida no MainWindowViewModel durante o boot via LicenseValidatorService.
    // Exposta como getter público para bindings XAML; setter interno ao ViewModel.
    private bool _isProVersion;
    public bool IsProVersion
    {
        get => _isProVersion;
        set => SetProperty(ref _isProVersion, value);
    }

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
