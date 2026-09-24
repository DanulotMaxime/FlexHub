using System.Windows;
using System.Windows.Threading;
using PersonalAppsHub.Services;

namespace PersonalAppsHub;

public partial class MonitoringAlertWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer = new()
    {
        Interval = TimeSpan.FromSeconds(10)
    };

    public event EventHandler? OpenMonitoringRequested;

    public MonitoringAlertWindow(string title, string message)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NonActivatingWindowService.Apply(this);
        AlertTitle.Text = title;
        AlertMessage.Text = message;
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 18;
            Top = area.Bottom - ActualHeight - 18;
            _autoCloseTimer.Start();
        };
        _autoCloseTimer.Tick += (_, _) => Close();
        Closed += (_, _) => _autoCloseTimer.Stop();
        DismissButton.Click += (_, _) => Close();
        OpenMonitoringButton.Click += (_, _) =>
        {
            OpenMonitoringRequested?.Invoke(this, EventArgs.Empty);
            Close();
        };
    }
}
