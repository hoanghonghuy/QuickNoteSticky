using System.IO;
using System.Text;
using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Service for handling drag and drop file operations
/// </summary>
public class FileDropService : IFileDropService
{
    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml"
    };
    
    private static readonly HashSet<string> CodeFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".py", ".ts", ".html", ".css", ".java", ".cpp", ".c", ".h", ".hpp",
        ".php", ".rb", ".go", ".rs", ".swift", ".kt", ".scala", ".sh", ".bat", ".ps1",
        ".sql", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf"
    };
    
    private static readonly HashSet<string> ImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico"
    };
    
    private static readonly Dictionary<string, string> LanguageMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "csharp" },
        { ".js", "javascript" },
        { ".ts", "typescript" },
        { ".py", "python" },
        { ".html", "html" },
        { ".css", "css" },
        { ".java", "java" },
        { ".cpp", "cpp" },
        { ".c", "c" },
        { ".h", "c" },
        { ".hpp", "cpp" },
        { ".php", "php" },
        { ".rb", "ruby" },
        { ".go", "go" },
        { ".rs", "rust" },
        { ".swift", "swift" },
        { ".kt", "kotlin" },
        { ".scala", "scala" },
        { ".sh", "bash" },
        { ".bat", "batch" },
        { ".ps1", "powershell" },
        { ".sql", "sql" },
        { ".yaml", "yaml" },
        { ".yml", "yaml" },
        { ".toml", "toml" },
        { ".json", "json" },
        { ".xml", "xml" }
    };

    /// <summary>
    /// Process a single dropped file and return formatted content
    /// </summary>
    public string ProcessDroppedFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return Path.GetFullPath(filePath ?? string.Empty);
        }

        var extension = GetFileExtension(filePath);
        
        try
        {
            if (IsImageFile(extension))
            {
                // Requirements 1.3: Insert markdown image link
                return $"![{Path.GetFileName(filePath)}]({Path.GetFullPath(filePath)})";
            }
            
            if (IsTextFile(extension) || IsCodeFile(extension))
            {
                var content = File.ReadAllText(filePath, Encoding.UTF8);
                
                if (IsCodeFile(extension))
                {
                    // Requirements 1.2: Wrap in markdown code block with language
                    var language = LanguageMapping.GetValueOrDefault(extension, "text");
                    return $"```{language}\n{content}\n```";
                }
                else
                {
                    // Requirements 1.1: Insert text file content directly
                    return content;
                }
            }
            
            // Requirements 1.4: Return absolute path for unsupported file types
            return Path.GetFullPath(filePath);
        }
        catch (Exception)
        {
            // If we can't read the file, return the path
            return Path.GetFullPath(filePath);
        }
    }

    /// <summary>
    /// Process multiple dropped files and return formatted content
    /// </summary>
    public string ProcessDroppedFiles(IEnumerable<string> filePaths)
    {
        if (filePaths == null)
        {
            return string.Empty;
        }

        var results = filePaths
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(ProcessDroppedFile)
            .Where(content => !string.IsNullOrEmpty(content))
            .ToList();

        // Requirements 1.5: Separate multiple files with blank lines
        return string.Join("\n\n", results);
    }

    /// <summary>
    /// Get the file extension from a file path
    /// </summary>
    public string GetFileExtension(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }
        
        return Path.GetExtension(filePath);
    }

    /// <summary>
    /// Check if a file extension represents a text file
    /// </summary>
    public bool IsTextFile(string extension)
    {
        return !string.IsNullOrEmpty(extension) && TextFileExtensions.Contains(extension);
    }

    /// <summary>
    /// Check if a file extension represents a code file
    /// </summary>
    public bool IsCodeFile(string extension)
    {
        return !string.IsNullOrEmpty(extension) && CodeFileExtensions.Contains(extension);
    }

    /// <summary>
    /// Check if a file extension represents an image file
    /// </summary>
    public bool IsImageFile(string extension)
    {
        return !string.IsNullOrEmpty(extension) && ImageFileExtensions.Contains(extension);
    }
}