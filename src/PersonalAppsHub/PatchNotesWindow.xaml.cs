using System.Windows;
using PersonalAppsHub.Services;

namespace PersonalAppsHub;

public partial class PatchNotesWindow : Window
{
    public PatchNotesWindow(VersionNote note)
    {
        InitializeComponent();
        VersionTitle.Text = $"Version {note.Version}";
        NotesText.Text = note.Notes;
        CloseButton.Click += (_, _) => Close();
    }
}
