using System.Net.Http;
using System.Windows;
using MonsterWebHost.Services;
using MonsterWebHost.ViewModels;
using Forms = System.Windows.Forms;

namespace MonsterWebHost;

public partial class MainWindow : Window
{
    private bool _startupCompleted;
    private HttpClient? _httpClient;
    private LogStore? _logStore;
    private AnalyticsStore? _analyticsStore;
    private WebsiteHostService? _websiteHostService;
    private CloudflaredService? _cloudflaredService;
    private FolderTreeBuilder? _folderTreeBuilder;
    private FolderMonitorService? _folderMonitorService;
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void MainWindow_ContentRendered(object sender, EventArgs e)
    {
        if (_startupCompleted)
        {
            return;
        }

        _startupCompleted = true;

        try
        {
            Title = "MonsterWebHost - Starting...";

            _httpClient = new HttpClient();

            _logStore = new LogStore();
            _analyticsStore = new AnalyticsStore();

            var geoLocationService = new GeoLocationService(_httpClient);
            _folderTreeBuilder = new FolderTreeBuilder();
            _folderMonitorService = new FolderMonitorService();
            _websiteHostService = new WebsiteHostService(_logStore, _analyticsStore, geoLocationService);
            _cloudflaredService = new CloudflaredService(_logStore);

            _viewModel = new MainViewModel(
                _websiteHostService,
                _cloudflaredService,
                _folderTreeBuilder,
                _folderMonitorService,
                _logStore);

            DataContext = _viewModel;
            Title = "MonsterWebHost";
        }
        catch (Exception ex)
        {
            Title = "MonsterWebHost - Startup failed";
            System.Windows.MessageBox.Show(
                this,
                ex.ToString(),
                "MonsterWebHost startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        await Task.CompletedTask;
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            if (_viewModel is not null)
            {
                await _viewModel.DisposeAsync();
            }
            else
            {
                _folderMonitorService?.Dispose();
                if (_websiteHostService is not null)
                {
                    await _websiteHostService.DisposeAsync();
                }

                if (_cloudflaredService is not null)
                {
                    _cloudflaredService.Dispose();
                }
            }
        }
        finally
        {
            _httpClient?.Dispose();
            _analyticsStore?.Dispose();
        }
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