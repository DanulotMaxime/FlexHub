using System.Windows;

namespace PersonalAppsHub;

public partial class ResponsePreviewWindow : Window
{
    public string Result => ResponseText.Text;

    public ResponsePreviewWindow(string response, string? title = null, string? description = null, string acceptLabel = "Remplacer")
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
            PreviewTitle.Text = title;
        }
        if (!string.IsNullOrWhiteSpace(description)) PreviewDescription.Text = description;
        AcceptButton.Content = acceptLabel;
        ResponseText.Text = response;
        ResponseText.SelectAll();
        ResponseText.Focus();
        Loaded += (_, _) => { Activate(); ResponseText.Focus(); };
        AcceptButton.Click += (_, _) => { DialogResult = true; Close(); };
        CancelButton.Click += (_, _) => { DialogResult = false; Close(); };
    }
}
