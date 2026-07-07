using System.Diagnostics;
using System.Windows;
using MonsterWebHost.ViewModels;
using Forms = System.Windows.Forms;

namespace MonsterWebHost;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select the folder that contains your website files",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        var result = dialog.ShowDialog();
        if (result == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            vm.SelectedFolder = dialog.SelectedPath;
        }
    }
}
