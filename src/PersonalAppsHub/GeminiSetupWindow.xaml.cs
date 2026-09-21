using System.Diagnostics;
using System.Windows;

namespace PersonalAppsHub;

public partial class GeminiSetupWindow : Window
{
    private const string GeminiApiKeysUrl = "https://aistudio.google.com/app/apikey";

    public string ApiKey => ApiKeyBox.Password.Trim();

    public GeminiSetupWindow() => InitializeComponent();

    private void OpenApiKeyPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GeminiApiKeysUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Impossible d’ouvrir la page Google Gemini : {ex.Message}", "Lien indisponible",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            System.Windows.MessageBox.Show(this, "Saisissez votre clé API Google Gemini avant de continuer.", "Clé API requise",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            ApiKeyBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
