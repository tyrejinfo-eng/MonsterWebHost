namespace MonsterWebHost.Services;

public sealed class FolderMonitorService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private string? _rootFolder;

    public event Action? FolderChanged;

    public void Start(string folderPath)
    {
        Stop();

        _rootFolder = folderPath;
        _watcher = new FileSystemWatcher(folderPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += OnFileSystemEvent;
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => Debounce();

    private void OnRenamed(object sender, RenamedEventArgs e) => Debounce();

    private void OnError(object sender, ErrorEventArgs e) => Debounce();

    private void Debounce()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            FolderChanged?.Invoke();
        }, null, TimeSpan.FromMilliseconds(350), Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileSystemEvent;
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Deleted -= OnFileSystemEvent;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _rootFolder = null;
    }

    public void Dispose() => Stop();
}
