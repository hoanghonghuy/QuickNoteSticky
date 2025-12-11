using DevSticky.Interfaces;
using System.IO;

namespace DevSticky.Services;

/// <summary>
/// Adapter for file system operations that wraps System.IO
/// </summary>
public class FileSystemAdapter : IFileSystem
{
    public async Task<string> ReadAllTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path).ConfigureAwait(false);
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public async Task DeleteFileAsync(string path)
    {
        await Task.Run(() => File.Delete(path)).ConfigureAwait(false);
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        File.Move(sourcePath, destinationPath);
    }

    public async Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        await Task.Run(() => File.Move(sourcePath, destinationPath)).ConfigureAwait(false);
    }

    public string? GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }
}
