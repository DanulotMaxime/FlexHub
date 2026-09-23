using System.Windows;

namespace PersonalAppsHub;

public partial class MonitoringAlertWindow : Window
{
    public event EventHandler? OpenMonitoringRequested;

    public MonitoringAlertWindow(string title, string message)
    {
        InitializeComponent();
        AlertTitle.Text = title;
        AlertMessage.Text = message;
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 18;
            Top = area.Bottom - ActualHeight - 18;
        };
        DismissButton.Click += (_, _) => Close();
        OpenMonitoringButton.Click += (_, _) =>
        {
            OpenMonitoringRequested?.Invoke(this, EventArgs.Empty);
            Close();
        };
    }
}
