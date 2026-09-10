using System.Windows;

namespace PersonalAppsHub;

public partial class MemorySetupWindow : Window
{
    public string MemoryType => Ddr5Option.IsChecked == true ? "DDR5" : "DDR4";
    public bool MonitoringEnabled => EnableMonitoringCheck.IsChecked == true;
    public bool UseCustomSpeed => CustomSpeedCheck.IsChecked == true;
    public int CustomSpeed { get; private set; }

    public MemorySetupWindow() => InitializeComponent();

    private void CustomSpeedChanged(object sender, RoutedEventArgs e) =>
        CustomSpeedBox.IsEnabled = CustomSpeedCheck.IsChecked == true;

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (UseCustomSpeed && (!int.TryParse(CustomSpeedBox.Text, out var speed) || speed is < 800 or > 20000))
        {
            System.Windows.MessageBox.Show(this, "La valeur personnalisée doit être comprise entre 800 et 20 000 MT/s.", "Valeur incorrecte", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CustomSpeed = UseCustomSpeed ? int.Parse(CustomSpeedBox.Text) : (MemoryType == "DDR5" ? 5800 : 3200);
        DialogResult = true;
    }
}
