using System.Windows;

namespace PersonalAppsHub;

public partial class CorrectionPreviewWindow : Window
{
    public string Result => CorrectedText.Text;
    public CorrectionPreviewWindow(string corrected)
    {
        InitializeComponent(); CorrectedText.Text = corrected;
        AcceptButton.Click += (_, _) => { DialogResult = true; Close(); };
        CancelButton.Click += (_, _) => { DialogResult = false; Close(); };
    }
}
