using System.Windows;

namespace PersonalAppsHub;

public partial class MonitoringSettingsWindow : Window
{
    public bool AlertsAreEnabled => AlertsEnabled.IsChecked == true;
    public int CpuAlertPercent { get; private set; }
    public int RamAlertPercent { get; private set; }
    public int GpuTemperatureAlertC { get; private set; }
    public event EventHandler? TestRequested;

    public MonitoringSettingsWindow(bool enabled, int cpu, int ram, int gpuTemperature)
    {
        InitializeComponent();
        AlertsEnabled.IsChecked = enabled;
        CpuThreshold.Text = cpu.ToString();
        RamThreshold.Text = ram.ToString();
        GpuTemperatureThreshold.Text = gpuTemperature.ToString();
        TestAlert.Click += (_, _) => TestRequested?.Invoke(this, EventArgs.Empty);
        Cancel.Click += (_, _) => DialogResult = false;
        Save.Click += (_, _) => SaveSettings();
    }

    private void SaveSettings()
    {
        if (!MainWindow.TryReadThreshold(CpuThreshold.Text, 50, 100, out var cpu) ||
            !MainWindow.TryReadThreshold(RamThreshold.Text, 50, 100, out var ram) ||
            !MainWindow.TryReadThreshold(GpuTemperatureThreshold.Text, 50, 110, out var gpuTemperature))
        {
            ValidationStatus.Text = "CPU et RAM : 50 à 100 %. Température GPU : 50 à 110 °C.";
            return;
        }
        CpuAlertPercent = cpu;
        RamAlertPercent = ram;
        GpuTemperatureAlertC = gpuTemperature;
        DialogResult = true;
    }
}
