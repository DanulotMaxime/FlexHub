using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
    private RuntimeDiagnosticsService? _runtimeDiagnostics;

    protected override void OnStartup(StartupEventArgs e)
    {
        var executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (ElevatedLaunchService.HasArgument(e.Args, ElevatedLaunchService.InstallArgument))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(executablePath)) throw new InvalidOperationException("Chemin de FlexHub introuvable.");
                ElevatedLaunchService.InstallForCurrentUser(executablePath);
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                AppLog.WriteException("INSTALLATION DU LANCEUR ADMINISTRATEUR IMPOSSIBLE", ex);
                Environment.ExitCode = 1;
            }
            Shutdown(Environment.ExitCode);
            return;
        }

        var launchedByElevatedTask = ElevatedLaunchService.HasArgument(e.Args, ElevatedLaunchService.ElevatedArgument);
        if (!launchedByElevatedTask && !ElevatedLaunchService.IsElevated && !string.IsNullOrWhiteSpace(executablePath))
        {
            if (ElevatedLaunchService.ShouldUseScheduledTask(executablePath))
            {
                if (ElevatedLaunchService.RunScheduledTask()) { Shutdown(); return; }
                AppLog.Write("LANCEUR ADMINISTRATEUR | Tâche indisponible, démarrage normal conservé.");
            }
            else if (ElevatedLaunchService.ShouldOfferSetup(executablePath))
            {
                var answer = System.Windows.MessageBox.Show(
                    "FlexHub peut demander une seule fois les droits administrateur afin d’accéder aux capteurs CPU " +
                    "et d’installer le pilote matériel signé PawnIO. " +
                    "Les prochains lancements utiliseront une tâche Windows sécurisée et ne redemanderont pas l’autorisation.\n\n" +
                    "Voulez-vous activer ce mode ?",
                    "Autorisation administrateur FlexHub", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (ElevatedLaunchService.RequestInstallation(executablePath) == 0 &&
                            ElevatedLaunchService.RunScheduledTask())
                        {
                            Shutdown();
                            return;
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                    {
                        AppLog.Write("LANCEUR ADMINISTRATEUR | Autorisation UAC annulée.");
                    }
                    catch (Exception ex) { AppLog.WriteException("LANCEUR ADMINISTRATEUR IMPOSSIBLE", ex); }
                }
                else ElevatedLaunchService.RememberDeclined(executablePath);
            }
        }

        if (MemoryCacheService.IsElevatedPurgeRequest(e.Args))
        {
            try
            {
                MemoryCacheService.PurgeStandbyList();
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                AppLog.Write($"PURGE CACHE RAM IMPOSSIBLE | {ex.Message}");
                Environment.ExitCode = 1;
            }
            Shutdown(Environment.ExitCode);
            return;
        }

        // FlexHub reste ouvert pendant de longues sessions, souvent en parallele de jeux
        // qui sollicitent fortement le pilote graphique. Le rendu materiel WPF peut alors
        // perdre son canal Direct3D et lever une OutOfMemoryException dans HwndTarget lors
        // de la restauration de la fenetre, meme lorsque la memoire du processus est basse.
        // Le rendu logiciel isole l'interface du pilote sans affecter les tests de charge
        // Direct3D, qui utilisent leur propre peripherique Vortice.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        AppLog.Write("RENDU WPF | Mode logiciel active pour la stabilite des longues sessions.");

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.WriteException("ERREUR WPF NON GÉRÉE", args.Exception);
            System.Windows.MessageBox.Show($"Une erreur inattendue s’est produite. Un diagnostic a été enregistré.\n\n{args.Exception.Message}", "FlexHub", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = args.Exception is not OutOfMemoryException;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception) AppLog.WriteException("ERREUR FATALE", exception);
            else AppLog.Write($"ERREUR FATALE | {AppLog.MemorySnapshot()} | {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) => { AppLog.WriteException("ERREUR ASYNCHRONE", args.Exception); args.SetObserved(); };
        _mutex = new Mutex(true, MutexName, out var created);
        _ownsMutex = created;
        if (!created && launchedByElevatedTask)
        {
            try
            {
                _ownsMutex = _mutex.WaitOne(TimeSpan.FromSeconds(10));
                created = _ownsMutex;
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
                created = true;
            }
        }
        if (!created)
        {
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
                showEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException ex)
            {
                AppLog.Write($"INSTANCE EXISTANTE INJOIGNABLE | {ex.Message}");
            }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        RepairStartupRegistration();
        _runtimeDiagnostics = new RuntimeDiagnosticsService();
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

    private static void RepairStartupRegistration()
    {
        try
        {
            var settings = new SettingsService().Load();
            StartupService.SetEnabled(settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            // Le démarrage de FlexHub ne doit jamais être bloqué par un registre
            // inaccessible. Le prochain enregistrement des paramètres réessaiera.
            AppLog.Write($"RÉPARATION DÉMARRAGE WINDOWS IMPOSSIBLE | {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true;
        _showEvent?.Set();
        _showEvent?.Dispose();
        _runtimeDiagnostics?.Dispose();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); }
            catch (ApplicationException ex) { AppLog.Write($"LIBÉRATION DU VERROU D'INSTANCE IMPOSSIBLE | {ex.Message}"); }
            finally { _ownsMutex = false; }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
