using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using MonsterWebHost.Models;
using MonsterWebHost.Services;

namespace MonsterWebHost.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WebsiteHostService _websiteHostService;
    private readonly CloudflaredService _cloudflaredService;
    private readonly FolderTreeBuilder _folderTreeBuilder;
    private readonly FolderMonitorService _folderMonitorService;
    private readonly LogStore _logStore;
    private readonly DispatcherTimer _statusTimer;

    private string _selectedFolder = string.Empty;
    private int _port = 8080;
    private bool _isHostRunning;
    private bool _isOnline;
    private string _statusMessage = "Ready.";
    private string _cloudflaredExecutable = "cloudflared";
    private string _cloudflaredArguments = "tunnel --url http://127.0.0.1:8080";
    private long _requestCount;
    private long _downloadCount;
    private long _bytesServed;
    private double _requestsPerSecond;
    private int _activeUsers;
    private DateTimeOffset? _lastRequestAt;
    private string _uptimeText = "00:00:00";
    private string _cloudflaredStatusText = "Cloudflared stopped";

    public MainViewModel(
        WebsiteHostService websiteHostService,
        CloudflaredService cloudflaredService,
        FolderTreeBuilder folderTreeBuilder,
        FolderMonitorService folderMonitorService,
        LogStore logStore)
    {
        _websiteHostService = websiteHostService;
        _cloudflaredService = cloudflaredService;
        _folderTreeBuilder = folderTreeBuilder;
        _folderMonitorService = folderMonitorService;
        _logStore = logStore;

        _websiteHostService.TrafficLogged += OnTrafficLogged;
        _folderMonitorService.FolderChanged += OnFolderChanged;
        _cloudflaredService.OutputReceived += OnCloudflaredOutput;

        FolderRoots = new ObservableCollection<FolderNode>();
        RecentEvents = new ObservableCollection<TrafficEvent>();

        StartHostCommand = new AsyncRelayCommand(StartHostAsync, CanStartHost);
        StopHostCommand = new AsyncRelayCommand(StopHostAsync, () => IsHostRunning);
        RefreshFolderCommand = new RelayCommand(RefreshFolder, () => !string.IsNullOrWhiteSpace(SelectedFolder));
        StartCloudflaredCommand = new AsyncRelayCommand(StartCloudflaredAsync, () => _websiteHostService.IsRunning);
        StopCloudflaredCommand = new AsyncRelayCommand(StopCloudflaredAsync, () => _cloudflaredService.IsRunning);
        OpenWebsiteCommand = new RelayCommand(OpenWebsite, () => IsHostRunning);

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _statusTimer.Tick += async (_, _) =>
        {
            try
            {
                await RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Status update failed: {ex.Message}";
            }
        };
        _statusTimer.Start();

        RaiseAll();
    }

    public ObservableCollection<FolderNode> FolderRoots { get; }
    public ObservableCollection<TrafficEvent> RecentEvents { get; }

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RefreshFolder();
                _folderMonitorService.Stop();
                if (!string.IsNullOrWhiteSpace(_selectedFolder) && Directory.Exists(_selectedFolder))
                {
                    _folderMonitorService.Start(_selectedFolder);
                }
                OnPropertyChanged(nameof(CanMonitorFolder));
                ((AsyncRelayCommand)StartHostCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshFolderCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                OnPropertyChanged(nameof(WebsiteUrl));
                ((AsyncRelayCommand)StartCloudflaredCommand).RaiseCanExecuteChanged();
                if (string.IsNullOrWhiteSpace(_cloudflaredArguments) || _cloudflaredArguments.Contains("127.0.0.1:8080", StringComparison.OrdinalIgnoreCase))
                {
                    CloudflaredArguments = $"tunnel --url http://127.0.0.1:{_port}";
                }
            }
        }
    }

    public string WebsiteUrl => $"http://127.0.0.1:{Port}";

    public string OnlineStatusText => IsOnline ? "Website is online." : "Website is offline.";
    public string CloudflaredStatusText
    {
        get => _cloudflaredStatusText;
        private set => SetProperty(ref _cloudflaredStatusText, value);
    }

    public bool IsHostRunning
    {
        get => _isHostRunning;
        private set
        {
            if (SetProperty(ref _isHostRunning, value))
            {
                OnPropertyChanged(nameof(OnlineStatusText));
                ((AsyncRelayCommand)StopHostCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)StartCloudflaredCommand).RaiseCanExecuteChanged();
                ((RelayCommand)OpenWebsiteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsOnline
    {
        get => _isOnline;
        private set
        {
            if (SetProperty(ref _isOnline, value))
            {
                OnPropertyChanged(nameof(OnlineStatusText));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CloudflaredExecutable
    {
        get => _cloudflaredExecutable;
        set => SetProperty(ref _cloudflaredExecutable, value);
    }

    public string CloudflaredArguments
    {
        get => _cloudflaredArguments;
        set => SetProperty(ref _cloudflaredArguments, value);
    }

    public long RequestCount
    {
        get => _requestCount;
        private set => SetProperty(ref _requestCount, value);
    }

    public long DownloadCount
    {
        get => _downloadCount;
        private set => SetProperty(ref _downloadCount, value);
    }

    public long BytesServed
    {
        get => _bytesServed;
        private set
        {
            if (SetProperty(ref _bytesServed, value))
            {
                OnPropertyChanged(nameof(BytesServedHuman));
            }
        }
    }

    public string BytesServedHuman => FormatBytes(BytesServed);

    public double RequestsPerSecond
    {
        get => _requestsPerSecond;
        private set => SetProperty(ref _requestsPerSecond, value);
    }

    public int ActiveUsers
    {
        get => _activeUsers;
        private set => SetProperty(ref _activeUsers, value);
    }

    public DateTimeOffset? LastRequestAt
    {
        get => _lastRequestAt;
        private set
        {
            if (SetProperty(ref _lastRequestAt, value))
            {
                OnPropertyChanged(nameof(LastRequestAtText));
            }
        }
    }

    public string LastRequestAtText => LastRequestAt?.ToLocalTime().ToString("g") ?? "-";

    public string UptimeText
    {
        get => _uptimeText;
        private set => SetProperty(ref _uptimeText, value);
    }

    public bool CanMonitorFolder => !string.IsNullOrWhiteSpace(SelectedFolder) && Directory.Exists(SelectedFolder);

    public AsyncRelayCommand StartHostCommand { get; }
    public AsyncRelayCommand StopHostCommand { get; }
    public RelayCommand RefreshFolderCommand { get; }
    public AsyncRelayCommand StartCloudflaredCommand { get; }
    public AsyncRelayCommand StopCloudflaredCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }

    private bool CanStartHost() => !string.IsNullOrWhiteSpace(SelectedFolder) && Directory.Exists(SelectedFolder) && !_websiteHostService.IsRunning;

    private async Task StartHostAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            StatusMessage = "Choose a website folder first.";
            return;
        }

        if (!Directory.Exists(SelectedFolder))
        {
            StatusMessage = "Selected folder does not exist.";
            return;
        }

        StatusMessage = "Starting local host...";
        try
        {
            await _websiteHostService.StartAsync(SelectedFolder, Port);
            IsHostRunning = true;
            IsOnline = true;
            StatusMessage = $"Hosting {SelectedFolder}";
            RefreshFolder();
            _folderMonitorService.Start(SelectedFolder);
            ((AsyncRelayCommand)StopHostCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)StartCloudflaredCommand).RaiseCanExecuteChanged();
            ((RelayCommand)OpenWebsiteCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)StartHostCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Host failed: {ex.Message}";
            IsHostRunning = false;
            IsOnline = false;
        }
    }

    private async Task StopHostAsync()
    {
        await _websiteHostService.StopAsync();
        IsHostRunning = false;
        IsOnline = false;
        StatusMessage = "Website host stopped.";
        _folderMonitorService.Stop();
        ((AsyncRelayCommand)StopHostCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StartCloudflaredCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenWebsiteCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StartHostCommand).RaiseCanExecuteChanged();
    }

    private void RefreshFolder()
    {
        FolderRoots.Clear();
        if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            return;
        }

        try
        {
            FolderRoots.Add(_folderTreeBuilder.Build(SelectedFolder));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Folder read failed: {ex.Message}";
        }
    }

    private async Task StartCloudflaredAsync()
    {
        if (!_websiteHostService.IsRunning)
        {
            StatusMessage = "Start the website host first.";
            return;
        }

        var args = string.IsNullOrWhiteSpace(CloudflaredArguments)
            ? $"tunnel --url {WebsiteUrl}"
            : CloudflaredArguments.Replace("http://127.0.0.1:8080", WebsiteUrl, StringComparison.OrdinalIgnoreCase);

        try
        {
            await _cloudflaredService.StartAsync(CloudflaredExecutable, args);
            CloudflaredStatusText = "Cloudflared running.";
            ((AsyncRelayCommand)StopCloudflaredCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            CloudflaredStatusText = $"Cloudflared failed: {ex.Message}";
        }
    }

    private async Task StopCloudflaredAsync()
    {
        await _cloudflaredService.StopAsync();
        CloudflaredStatusText = "Cloudflared stopped.";
        ((AsyncRelayCommand)StopCloudflaredCommand).RaiseCanExecuteChanged();
    }

    private void OpenWebsite()
    {
        if (!IsHostRunning)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WebsiteUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusMessage = "Could not open browser.";
        }
    }

    private async Task RefreshStatusAsync()
    {
        if (_websiteHostService.IsRunning)
        {
            IsOnline = await _websiteHostService.PingHealthAsync();
        }
        else
        {
            IsOnline = false;
        }

        CloudflaredStatusText = _cloudflaredService.IsRunning ? "Cloudflared running." : "Cloudflared stopped.";

        var metrics = _websiteHostService.GetMetrics();
        RequestCount = metrics.RequestCount;
        DownloadCount = metrics.DownloadCount;
        BytesServed = metrics.BytesServed;
        RequestsPerSecond = metrics.RequestsPerSecond;
        ActiveUsers = metrics.ActiveUsers;
        LastRequestAt = metrics.LastRequestAtUtc;
        UptimeText = FormatTimeSpan(metrics.Uptime);
    }

    private void OnTrafficLogged(object? sender, TrafficEvent e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            RequestCount += 1;
            BytesServed += e.ResponseBytes;
            LastRequestAt = e.TimestampUtc;
            if (e.IsDownload)
            {
                DownloadCount += 1;
            }

            RecentEvents.Insert(0, e);
            while (RecentEvents.Count > 100)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }

            StatusMessage = $"{e.RequestMethod} {e.RequestPath} -> {e.StatusCode}";
        });
    }

    private void OnFolderChanged()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(RefreshFolder);
    }

    private void OnCloudflaredOutput(string line)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CloudflaredStatusText = line;
        });
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(WebsiteUrl));
        OnPropertyChanged(nameof(OnlineStatusText));
        OnPropertyChanged(nameof(CloudflaredStatusText));
        OnPropertyChanged(nameof(BytesServedHuman));
        OnPropertyChanged(nameof(LastRequestAtText));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(CanMonitorFolder));
        ((AsyncRelayCommand)StopHostCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StartCloudflaredCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)StopCloudflaredCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RefreshFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenWebsiteCommand).RaiseCanExecuteChanged();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {suffixes[order]}";
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan < TimeSpan.Zero)
        {
            timeSpan = TimeSpan.Zero;
        }

        return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
    }

    public async ValueTask DisposeAsync()
    {
        _statusTimer.Stop();
        _folderMonitorService.Stop();
        await _websiteHostService.DisposeAsync();
        await _cloudflaredService.StopAsync();
        _ = _logStore.AppendMessageAsync("MainViewModel disposed");
    }
}
