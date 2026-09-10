using System.Windows;

namespace PersonalAppsHub;

public partial class ResponsePreviewWindow : Window
{
    public string Result => ResponseText.Text;

    public ResponsePreviewWindow(string response)
    {
        InitializeComponent();
        ResponseText.Text = response;
        ResponseText.SelectAll();
        ResponseText.Focus();
        Loaded += (_, _) => { Activate(); ResponseText.Focus(); };
        AcceptButton.Click += (_, _) => { DialogResult = true; Close(); };
        CancelButton.Click += (_, _) => { DialogResult = false; Close(); };
    }
}
