namespace DevSticky.Interfaces;

/// <summary>
/// Abstraction for file system operations to enable testability
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Reads all text from a file asynchronously
    /// </summary>
    Task<string> ReadAllTextAsync(string path);

    /// <summary>
    /// Writes all text to a file asynchronously
    /// </summary>
    Task WriteAllTextAsync(string path, string content);

    /// <summary>
    /// Checks if a file exists
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Checks if a directory exists
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Creates a directory and all necessary parent directories
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Deletes a file
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes a file asynchronously
    /// </summary>
    Task DeleteFileAsync(string path);

    /// <summary>
    /// Moves a file from source to destination
    /// </summary>
    void MoveFile(string sourcePath, string destinationPath);

    /// <summary>
    /// Moves a file from source to destination asynchronously
    /// </summary>
    Task MoveFileAsync(string sourcePath, string destinationPath);

    /// <summary>
    /// Gets the directory name from a path
    /// </summary>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Combines path segments
    /// </summary>
    string Combine(params string[] paths);
}
