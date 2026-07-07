using System.IO;
using MonsterWebHost.Models;

namespace MonsterWebHost.Services;

public sealed class FolderTreeBuilder
{
    public FolderNode Build(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(rootPath));
        }

        var rootDirectory = new DirectoryInfo(rootPath);
        if (!rootDirectory.Exists)
        {
            throw new DirectoryNotFoundException(rootPath);
        }

        return BuildDirectory(rootDirectory);
    }

    private static FolderNode BuildDirectory(DirectoryInfo directoryInfo)
    {
        var node = new FolderNode(directoryInfo.Name, directoryInfo.FullName, true);

        foreach (var directory in SafeEnumerateDirectories(directoryInfo))
        {
            node.Children.Add(BuildDirectory(directory));
        }

        foreach (var file in SafeEnumerateFiles(directoryInfo))
        {
            node.Children.Add(new FolderNode(file.Name, file.FullName, false));
        }

        return node;
    }

    private static IEnumerable<DirectoryInfo> SafeEnumerateDirectories(DirectoryInfo directoryInfo)
    {
        try
        {
            return directoryInfo.EnumerateDirectories().OrderBy(d => d.Name);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<FileInfo> SafeEnumerateFiles(DirectoryInfo directoryInfo)
    {
        try
        {
            return directoryInfo.EnumerateFiles().OrderBy(f => f.Name);
        }
        catch
        {
            return [];
        }
    }
}
