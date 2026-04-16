using System;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BackupDriver.Infrastructure;

namespace BackupDriver.App.ViewModels;

public partial class LogEntryViewModel : ViewModelBase
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string LevelIcon => Level switch
    {
        "OK" => "🟢",
        "ERROR" => "🔴",
        "WARN" => "🟡",
        _ => "⚪"
    };

    public string DisplayText => $"[{Timestamp}] [{Level}] {Message}";
}

public partial class LogsViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly string _logDirectory;

    [ObservableProperty]
    private ObservableCollection<LogEntryViewModel> _logs = new();

    [ObservableProperty]
    private string _filterLevel = "Todos";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _displayCount = "Exibindo 0 entradas";

    public LogsViewModel(ILogger logger)
    {
        _logger = logger;
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        LoadLogs();
    }

    [RelayCommand]
    private void LoadLogs()
    {
        var allLogs = _logger.GetRecentLogs(1000);

        var filtered = allLogs;

        if (FilterLevel != "Todos")
        {
            filtered = filtered.Where(l => l.Level == FilterLevel).ToList();
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            filtered = filtered.Where(l => l.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        Logs.Clear();
        foreach (var log in filtered)
        {
            Logs.Add(new LogEntryViewModel
            {
                Timestamp = log.Timestamp,
                Level = log.Level,
                Message = log.Message
            });
        }

        DisplayCount = $"Exibindo {Logs.Count} de {allLogs.Count} entradas";
    }

    [RelayCommand]
    private void FilterAll()
    {
        FilterLevel = "Todos";
        LoadLogs();
    }

    [RelayCommand]
    private void FilterErrors()
    {
        FilterLevel = "ERROR";
        LoadLogs();
    }

    [RelayCommand]
    private void FilterSuccess()
    {
        FilterLevel = "OK";
        LoadLogs();
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        if (Directory.Exists(_logDirectory))
        {
            Process.Start("explorer.exe", _logDirectory);
        }
    }

    [RelayCommand]
    private void Search()
    {
        LoadLogs();
    }
}
