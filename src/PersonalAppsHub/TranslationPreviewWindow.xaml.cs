using System.Windows;

namespace PersonalAppsHub;

public partial class TranslationPreviewWindow : Window
{
    public string Result => TranslatedText.Text;
    public TranslationPreviewWindow(string translated)
    {
        InitializeComponent();
        TranslatedText.Text = translated;
        AcceptButton.Click += (_, _) => { DialogResult = true; Close(); };
        CancelButton.Click += (_, _) => { DialogResult = false; Close(); };
    }
}
