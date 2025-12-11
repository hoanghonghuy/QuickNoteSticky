using System.IO;

namespace DevSticky.Helpers;

/// <summary>
/// Helper class for file path operations.
/// Provides utilities for working with file and directory paths.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Combines multiple path segments into a single path.
    /// </summary>
    /// <param name="paths">The path segments to combine</param>
    /// <returns>The combined path</returns>
    public static string Combine(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return string.Empty;

        return Path.Combine(paths);
    }

    /// <summary>
    /// Gets the application data folder path for the application.
    /// </summary>
    /// <param name="appName">The application name</param>
    /// <returns>The full path to the application data folder</returns>
    public static string GetAppDataPath(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name cannot be null or empty", nameof(appName));

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, appName);
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="path">The directory path</param>
    /// <returns>True if the directory exists or was created; otherwise false</returns>
    public static bool EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the directory name from a file path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>The directory name or empty string if invalid</returns>
    public static string GetDirectoryName(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetDirectoryName(filePath) ?? string.Empty;
    }

    /// <summary>
    /// Gets the file name from a file path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>The file name or empty string if invalid</returns>
    public static string GetFileName(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetFileName(filePath);
    }

    /// <summary>
    /// Gets the file name without extension from a file path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>The file name without extension or empty string if invalid</returns>
    public static string GetFileNameWithoutExtension(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// Gets the file extension from a file path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>The file extension (including the dot) or empty string if invalid</returns>
    public static string GetExtension(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetExtension(filePath);
    }

    /// <summary>
    /// Changes the extension of a file path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="newExtension">The new extension (with or without dot)</param>
    /// <returns>The file path with the new extension</returns>
    public static string ChangeExtension(string filePath, string newExtension)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.ChangeExtension(filePath, newExtension);
    }

    /// <summary>
    /// Generates a unique file path by appending a number if the file already exists.
    /// </summary>
    /// <param name="filePath">The desired file path</param>
    /// <returns>A unique file path</returns>
    public static string GetUniqueFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            return filePath;

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        int counter = 1;
        string newPath;
        do
        {
            var newFileName = $"{fileNameWithoutExtension} ({counter}){extension}";
            newPath = Path.Combine(directory, newFileName);
            counter++;
        }
        while (File.Exists(newPath));

        return newPath;
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    /// <param name="fileName">The file name to sanitize</param>
    /// <param name="replacement">The replacement character for invalid characters</param>
    /// <returns>A sanitized file name</returns>
    public static string SanitizeFileName(string fileName, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, replacement);
        }

        return sanitized;
    }

    /// <summary>
    /// Checks if a path is a valid file path format.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is valid; otherwise false</returns>
    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the relative path from one path to another.
    /// </summary>
    /// <param name="fromPath">The source path</param>
    /// <param name="toPath">The destination path</param>
    /// <returns>The relative path</returns>
    public static string GetRelativePath(string fromPath, string toPath)
    {
        if (string.IsNullOrWhiteSpace(fromPath))
            throw new ArgumentException("From path cannot be null or empty", nameof(fromPath));
        if (string.IsNullOrWhiteSpace(toPath))
            throw new ArgumentException("To path cannot be null or empty", nameof(toPath));

        return Path.GetRelativePath(fromPath, toPath);
    }

    /// <summary>
    /// Normalizes path separators to the current platform's separator.
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>The normalized path</returns>
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path.Replace('\\', Path.DirectorySeparatorChar)
                   .Replace('/', Path.DirectorySeparatorChar);
    }
}
