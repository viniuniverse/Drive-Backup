using System.Threading.Tasks;

namespace BackupDriver.Core.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string title);
    Task<string?> PickFileAsync(string title, string[] extensions);
}