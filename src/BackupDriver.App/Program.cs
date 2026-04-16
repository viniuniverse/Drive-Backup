using Avalonia;
using System;

namespace BackupDriver.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Mutex nomeado para controle de instância única
        const string mutexName = "BackupDriver-SingleInstance-Mutex";
        using var mutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Já existe uma instância rodando, aborta silenciosamente
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
