using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace Island;

public partial class App : Application
{
    private static string LogDir => Services.SettingsStore.Dir;

    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, "IslandWidget.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        Services.LogRotation.Rotate();
        Services.ToastService.Initialize();

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);
    }

    private static void Log(Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(Path.Combine(LogDir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch
        {
            // nothing else we can do here
        }
    }
}