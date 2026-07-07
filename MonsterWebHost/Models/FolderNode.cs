using System.Collections.ObjectModel;
using MonsterWebHost.ViewModels;

namespace MonsterWebHost.Models;

public sealed class FolderNode : ObservableObject
{
    private string _name = string.Empty;

    public FolderNode(string name, string fullPath, bool isDirectory)
    {
        _name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public ObservableCollection<FolderNode> Children { get; } = new();

    public string DisplayName => IsDirectory ? Name : $"{Name}";

    public override string ToString() => DisplayName;
}
