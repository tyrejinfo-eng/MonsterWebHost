using System.Windows;
using MonsterWebHost.Services;
using MonsterWebHost.ViewModels;

namespace MonsterWebHost;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var logStore = new LogStore();
        var geoLocationService = new GeoLocationService(new HttpClient());
        var folderTreeBuilder = new FolderTreeBuilder();
        var folderMonitorService = new FolderMonitorService();
        var websiteHostService = new WebsiteHostService(logStore, geoLocationService);
        var cloudflaredService = new CloudflaredService(logStore);

        _viewModel = new MainViewModel(
            websiteHostService,
            cloudflaredService,
            folderTreeBuilder,
            folderMonitorService,
            logStore);

        var window = new MainWindow
        {
            DataContext = _viewModel
        };

        MainWindow = window;
        window.Show();
    }

    private async void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
        }
    }
}
