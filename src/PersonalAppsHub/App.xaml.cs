using System.Threading;
using System.Windows;
using PersonalAppsHub.Services;

namespace PersonalAppsHub;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\PersonalAppsHub.CSharp";
    private const string ShowEventName = "Local\\PersonalAppsHub.Show";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private volatile bool _exiting;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write($"ERREUR WPF NON GÉRÉE: {args.Exception}");
            System.Windows.MessageBox.Show($"Une erreur inattendue s’est produite. Un diagnostic a été enregistré.\n\n{args.Exception.Message}", "Personal Apps Hub", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => AppLog.Write($"ERREUR FATALE: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) => { AppLog.Write($"ERREUR ASYNCHRONE: {args.Exception}"); args.SetObserved(); };
        _mutex = new Mutex(true, MutexName, out var created);
        _ownsMutex = created;
        if (!created)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _mainWindow = new MainWindow();
        _mainWindow.Show();
        _ = Task.Run(() =>
        {
            while (!_exiting)
            {
                _showEvent.WaitOne();
                if (!_exiting) Dispatcher.Invoke(() => _mainWindow?.ShowFromSecondaryLaunch());
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        _showEvent?.Set();
        _showEvent?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
