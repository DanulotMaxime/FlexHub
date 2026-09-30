using System.Windows;
using PersonalAppsHub.Models;

namespace PersonalAppsHub;

public partial class AlertCenterWindow : Window
{
    public bool ReminderEnabledValue { get; private set; }
    public int ReminderIntervalMinutes { get; private set; }
    public int ReminderDisplaySeconds { get; private set; }
    public bool MonitoringEnabledValue { get; private set; }
    public int CpuAlertPercent { get; private set; }
    public int RamAlertPercent { get; private set; }
    public int GpuTemperatureAlertC { get; private set; }
    public bool GameAlertEnabledValue { get; private set; }
    public int GameAlertHours { get; private set; }
    public bool JitterEnabledValue { get; private set; }
    public int JitterAlertMs { get; private set; }
    public bool XmpEnabledValue { get; private set; }

    public AlertCenterWindow(HubSettings settings)
    {
        InitializeComponent();
        ReminderAlertEnabled.IsChecked = settings.ReminderEnabled;
        ReminderMinutes.Text = settings.ReminderIntervalMinutes.ToString();
        ReminderSeconds.Text = settings.ReminderDisplaySeconds.ToString();
        MonitoringAlertsEnabled.IsChecked = settings.MonitoringAlertsEnabled;
        CpuThreshold.Text = settings.MonitoringCpuAlertPercent.ToString();
        RamThreshold.Text = settings.MonitoringRamAlertPercent.ToString();
        GpuThreshold.Text = settings.MonitoringGpuTemperatureAlertC.ToString();
        GameSessionAlertEnabled.IsChecked = settings.GameSessionAlertEnabled;
        GameSessionHours.Text = settings.GameSessionAlertHours.ToString();
        JitterAlertEnabled.IsChecked = settings.NetworkJitterAlertEnabled;
        JitterThreshold.Text = settings.NetworkJitterAlertMs.ToString();
        XmpAlertEnabled.IsChecked = settings.XmpMonitorEnabled;

        ReminderAlertEnabled.Checked += (_, _) => RefreshEnabledFields();
        ReminderAlertEnabled.Unchecked += (_, _) => RefreshEnabledFields();
        MonitoringAlertsEnabled.Checked += (_, _) => RefreshEnabledFields();
        MonitoringAlertsEnabled.Unchecked += (_, _) => RefreshEnabledFields();
        GameSessionAlertEnabled.Checked += (_, _) => RefreshEnabledFields();
        GameSessionAlertEnabled.Unchecked += (_, _) => RefreshEnabledFields();
        JitterAlertEnabled.Checked += (_, _) => RefreshEnabledFields();
        JitterAlertEnabled.Unchecked += (_, _) => RefreshEnabledFields();
        RefreshEnabledFields();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryRead(ReminderMinutes.Text, 1, 1440, "L’intervalle du rappel doit être compris entre 1 et 1440 minutes.", out var reminderMinutes) ||
            !TryRead(ReminderSeconds.Text, 1, 300, "La durée du rappel doit être comprise entre 1 et 300 secondes.", out var reminderSeconds) ||
            !TryRead(CpuThreshold.Text, 50, 100, "Le seuil CPU doit être compris entre 50 et 100 %.", out var cpu) ||
            !TryRead(RamThreshold.Text, 50, 100, "Le seuil RAM doit être compris entre 50 et 100 %.", out var ram) ||
            !TryRead(GpuThreshold.Text, 50, 100, "Le seuil GPU doit être compris entre 50 et 100 °C.", out var gpu) ||
            !TryRead(GameSessionHours.Text, 1, 24, "La durée de jeu doit être comprise entre 1 et 24 heures.", out var gameHours) ||
            !TryRead(JitterThreshold.Text, 5, 200, "Le seuil de jitter doit être compris entre 5 et 200 ms.", out var jitter)) return;

        ReminderEnabledValue = ReminderAlertEnabled.IsChecked == true;
        ReminderIntervalMinutes = reminderMinutes;
        ReminderDisplaySeconds = reminderSeconds;
        MonitoringEnabledValue = MonitoringAlertsEnabled.IsChecked == true;
        CpuAlertPercent = cpu;
        RamAlertPercent = ram;
        GpuTemperatureAlertC = gpu;
        GameAlertEnabledValue = GameSessionAlertEnabled.IsChecked == true;
        GameAlertHours = gameHours;
        JitterEnabledValue = JitterAlertEnabled.IsChecked == true;
        JitterAlertMs = jitter;
        XmpEnabledValue = XmpAlertEnabled.IsChecked == true;
        DialogResult = true;
    }

    private bool TryRead(string text, int minimum, int maximum, string message, out int value)
    {
        if (int.TryParse(text.Trim(), out value) && value >= minimum && value <= maximum) return true;
        ValidationText.Text = message;
        return false;
    }

    private void RefreshEnabledFields()
    {
        ReminderMinutes.IsEnabled = ReminderAlertEnabled.IsChecked == true;
        ReminderSeconds.IsEnabled = ReminderAlertEnabled.IsChecked == true;
        CpuThreshold.IsEnabled = MonitoringAlertsEnabled.IsChecked == true;
        RamThreshold.IsEnabled = MonitoringAlertsEnabled.IsChecked == true;
        GpuThreshold.IsEnabled = MonitoringAlertsEnabled.IsChecked == true;
        GameSessionHours.IsEnabled = GameSessionAlertEnabled.IsChecked == true;
        JitterThreshold.IsEnabled = JitterAlertEnabled.IsChecked == true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
