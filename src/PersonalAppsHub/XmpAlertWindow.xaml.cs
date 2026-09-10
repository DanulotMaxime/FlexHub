using System.Diagnostics;
using System.Windows;

namespace PersonalAppsHub;

public partial class XmpAlertWindow : Window
{
    public XmpAlertWindow(int detectedSpeed, int expectedSpeed)
    {
        InitializeComponent();
        AlertMessage.Text = $"La RAM fonctionne à {detectedSpeed} MT/s au lieu de {expectedSpeed} MT/s. Activez XMP dans le BIOS, puis redémarrez l’ordinateur.";
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 18;
            Top = area.Bottom - ActualHeight - 18;
        };
        DismissButton.Click += (_, _) => Close();
        RestartButton.Click += (_, _) =>
        {
            if (System.Windows.MessageBox.Show(this, "Redémarrer Windows maintenant ? Enregistrez d’abord votre travail ouvert.", "Confirmer le redémarrage", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = true });
        };
    }
}
