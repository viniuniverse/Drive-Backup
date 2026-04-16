using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using BackupDriver.Core.Services;

namespace BackupDriver.App.Services;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].TryGetLocalPath() ?? result[0].Path.AbsolutePath : null;
    }

    public async Task<string?> PickFileAsync(string title, string[] extensions)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var patterns = extensions.Select(e => new FilePickerFileType(e)
        {
            Patterns = new[] { e }
        }).ToList();

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = patterns
        };

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].TryGetLocalPath() ?? result[0].Path.AbsolutePath : null;
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}