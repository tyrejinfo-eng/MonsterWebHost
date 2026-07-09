namespace MonsterWebHost;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show(
                args.Exception.ToString(),
                "MonsterWebHost startup error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var message = args.ExceptionObject?.ToString() ?? "Unknown fatal error.";
            System.Windows.MessageBox.Show(
                message,
                "MonsterWebHost fatal error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        };

        base.OnStartup(e);
    }
}
