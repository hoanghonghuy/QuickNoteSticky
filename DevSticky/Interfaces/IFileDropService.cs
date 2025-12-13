namespace DevSticky.Interfaces;

/// <summary>
/// Service for handling drag and drop file operations
/// </summary>
public interface IFileDropService
{
    /// <summary>
    /// Process a single dropped file and return formatted content
    /// </summary>
    /// <param name="filePath">Path to the dropped file</param>
    /// <returns>Formatted content to insert into note</returns>
    string ProcessDroppedFile(string filePath);
    
    /// <summary>
    /// Process multiple dropped files and return formatted content
    /// </summary>
    /// <param name="filePaths">Paths to the dropped files</param>
    /// <returns>Formatted content to insert into note, with files separated by blank lines</returns>
    string ProcessDroppedFiles(IEnumerable<string> filePaths);
    
    /// <summary>
    /// Get the file extension from a file path
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File extension (including the dot)</returns>
    string GetFileExtension(string filePath);
    
    /// <summary>
    /// Check if a file extension represents a text file
    /// </summary>
    /// <param name="extension">File extension (including the dot)</param>
    /// <returns>True if it's a text file</returns>
    bool IsTextFile(string extension);
    
    /// <summary>
    /// Check if a file extension represents a code file
    /// </summary>
    /// <param name="extension">File extension (including the dot)</param>
    /// <returns>True if it's a code file</returns>
    bool IsCodeFile(string extension);
    
    /// <summary>
    /// Check if a file extension represents an image file
    /// </summary>
    /// <param name="extension">File extension (including the dot)</param>
    /// <returns>True if it's an image file</returns>
    bool IsImageFile(string extension);
}