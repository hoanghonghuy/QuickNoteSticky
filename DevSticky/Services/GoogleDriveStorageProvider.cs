using System.IO;
using System.Text.Json;
using DevSticky.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace DevSticky.Services;

/// <summary>
/// Cloud storage provider implementation for Google Drive.
/// Uses Google Drive API v3 with OAuth 2.0 authentication.
/// </summary>
public class GoogleDriveStorageProvider : ICloudStorageProvider
{
    // Google Cloud Console application credentials
    // Note: In production, these should be configured externally
    private const string ClientId = "YOUR_GOOGLE_CLIENT_ID"; // Replace with actual client ID
    private const string ClientSecret = "YOUR_GOOGLE_CLIENT_SECRET"; // Replace with actual client secret
    
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
    
    private const string CredentialKey = "GoogleDrive_Token";
    private const string DevStickyFolderName = "DevSticky";
    private const string ApplicationName = "DevSticky";
    
    private DriveService? _driveService;
    private string? _devStickyFolderId;
    private bool _isAuthenticated;
    private bool _disposed;

    /// <inheritdoc />
    public string ProviderName => "Google Drive";

    /// <inheritdoc />
    public bool IsAuthenticated => _isAuthenticated;

    /// <inheritdoc />
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            // Try to load existing token from credential manager
            var storedToken = CredentialManagerService.GetCredential(CredentialKey);
            UserCredential? credential = null;

            if (!string.IsNullOrEmpty(storedToken))
            {
                try
                {
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(storedToken);
                    if (tokenResponse != null)
                    {
                        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = new ClientSecrets
                            {
                                ClientId = ClientId,
                                ClientSecret = ClientSecret
                            },
                            Scopes = Scopes
                        });

                        credential = new UserCredential(flow, "user", tokenResponse);
                        
                        // Try to refresh if expired
                        if (credential.Token.IsStale)
                        {
                            await credential.RefreshTokenAsync(CancellationToken.None);
                            SaveToken(credential.Token);
                        }
                    }
                }
                catch
                {
                    // Token invalid, need to re-authenticate
                    credential = null;
                }
            }

            // If no valid stored token, do interactive authentication
            if (credential == null)
            {
                var dataStore = new FileDataStore(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "DevSticky", "google_credentials"), 
                    true);

                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets
                    {
                        ClientId = ClientId,
                        ClientSecret = ClientSecret
                    },
                    Scopes,
                    "user",
                    CancellationToken.None,
                    dataStore);

                // Save token to credential manager
                SaveToken(credential.Token);
            }

            // Create Drive service
            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });

            _isAuthenticated = true;

            // Ensure DevSticky folder exists
            await EnsureDevStickyFolderExistsAsync();

            return true;
        }
        catch (Exception)
        {
            _isAuthenticated = false;
            return false;
        }
    }

    private void SaveToken(TokenResponse token)
    {
        try
        {
            var tokenJson = JsonSerializer.Serialize(token);
            CredentialManagerService.SaveCredential(CredentialKey, tokenJson);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <inheritdoc />
    public Task SignOutAsync()
    {
        CredentialManagerService.DeleteCredential(CredentialKey);
        
        // Delete local credential store
        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevSticky", "google_credentials");
        
        if (Directory.Exists(credPath))
        {
            try
            {
                Directory.Delete(credPath, true);
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        _driveService?.Dispose();
        _driveService = null;
        _devStickyFolderId = null;
        _isAuthenticated = false;

        return Task.CompletedTask;
    }

    private async Task EnsureDevStickyFolderExistsAsync()
    {
        if (_driveService == null) return;

        // Search for existing DevSticky folder
        var request = _driveService.Files.List();
        request.Q = $"name = '{DevStickyFolderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        request.Fields = "files(id, name)";

        var result = await request.ExecuteAsync();
        
        if (result.Files.Count > 0)
        {
            _devStickyFolderId = result.Files[0].Id;
        }
        else
        {
            // Create the folder
            var folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = DevStickyFolderName,
                MimeType = "application/vnd.google-apps.folder"
            };

            var createRequest = _driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            var folder = await createRequest.ExecuteAsync();
            _devStickyFolderId = folder.Id;
        }
    }

    private async Task<string?> GetFileIdAsync(string remotePath)
    {
        if (_driveService == null || string.IsNullOrEmpty(_devStickyFolderId))
            return null;

        var parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentFolderId = _devStickyFolderId;

        for (int i = 0; i < parts.Length; i++)
        {
            var isLastPart = i == parts.Length - 1;
            var name = parts[i];

            var request = _driveService.Files.List();
            request.Q = $"name = '{EscapeQueryString(name)}' and '{currentFolderId}' in parents and trashed = false";
            request.Fields = "files(id, name, mimeType)";

            var result = await request.ExecuteAsync();
            
            if (result.Files.Count == 0)
                return null;

            var file = result.Files[0];
            
            if (isLastPart)
                return file.Id;

            // Must be a folder to continue
            if (file.MimeType != "application/vnd.google-apps.folder")
                return null;

            currentFolderId = file.Id;
        }

        return null;
    }

    private async Task<string?> GetOrCreateFolderIdAsync(string folderPath)
    {
        if (_driveService == null || string.IsNullOrEmpty(_devStickyFolderId))
            return null;

        if (string.IsNullOrEmpty(folderPath))
            return _devStickyFolderId;

        var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentFolderId = _devStickyFolderId;

        foreach (var name in parts)
        {
            var request = _driveService.Files.List();
            request.Q = $"name = '{EscapeQueryString(name)}' and '{currentFolderId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name)";

            var result = await request.ExecuteAsync();

            if (result.Files.Count > 0)
            {
                currentFolderId = result.Files[0].Id;
            }
            else
            {
                // Create folder
                var folderMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = name,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { currentFolderId }
                };

                var createRequest = _driveService.Files.Create(folderMetadata);
                createRequest.Fields = "id";
                var folder = await createRequest.ExecuteAsync();
                currentFolderId = folder.Id;
            }
        }

        return currentFolderId;
    }

    private static string EscapeQueryString(string value)
    {
        return value.Replace("'", "\\'");
    }

    /// <inheritdoc />
    public async Task<string?> UploadFileAsync(string remotePath, byte[] content)
    {
        if (_driveService == null || !_isAuthenticated || string.IsNullOrEmpty(_devStickyFolderId))
            return null;

        try
        {
            // Get parent folder path and file name
            var lastSlash = remotePath.LastIndexOf('/');
            var parentPath = lastSlash > 0 ? remotePath[..lastSlash] : "";
            var fileName = lastSlash >= 0 ? remotePath[(lastSlash + 1)..] : remotePath;

            // Get or create parent folder
            var parentFolderId = await GetOrCreateFolderIdAsync(parentPath);
            if (parentFolderId == null)
                return null;

            // Check if file already exists
            var existingFileId = await GetFileIdAsync(remotePath);

            using var stream = new MemoryStream(content);

            if (existingFileId != null)
            {
                // Update existing file
                var updateRequest = _driveService.Files.Update(
                    new Google.Apis.Drive.v3.Data.File(),
                    existingFileId,
                    stream,
                    "application/octet-stream");
                updateRequest.Fields = "id, version";
                
                var updatedFile = await updateRequest.UploadAsync();
                return updateRequest.ResponseBody?.Id;
            }
            else
            {
                // Create new file
                var fileMetadata = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName,
                    Parents = new List<string> { parentFolderId }
                };

                var createRequest = _driveService.Files.Create(
                    fileMetadata,
                    stream,
                    "application/octet-stream");
                createRequest.Fields = "id, version";

                await createRequest.UploadAsync();
                return createRequest.ResponseBody?.Id;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadFileAsync(string remotePath)
    {
        if (_driveService == null || !_isAuthenticated)
            return null;

        try
        {
            var fileId = await GetFileIdAsync(remotePath);
            if (fileId == null)
                return null;

            using var stream = new MemoryStream();
            var request = _driveService.Files.Get(fileId);
            await request.DownloadAsync(stream);
            
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string remotePath)
    {
        if (_driveService == null || !_isAuthenticated)
            return false;

        try
        {
            var fileId = await GetFileIdAsync(remotePath);
            if (fileId == null)
                return true; // File doesn't exist, consider it deleted

            await _driveService.Files.Delete(fileId).ExecuteAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string remotePath)
    {
        if (_driveService == null || !_isAuthenticated || string.IsNullOrEmpty(_devStickyFolderId))
            return Array.Empty<CloudFileInfo>();

        try
        {
            var folderId = string.IsNullOrEmpty(remotePath) 
                ? _devStickyFolderId 
                : await GetFileIdAsync(remotePath);

            if (folderId == null)
                return Array.Empty<CloudFileInfo>();

            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name, size, modifiedTime, mimeType, version)";

            var result = await request.ExecuteAsync();

            return result.Files.Select(file => new CloudFileInfo
            {
                Name = file.Name,
                Path = string.IsNullOrEmpty(remotePath) ? file.Name : $"{remotePath}/{file.Name}",
                Size = file.Size ?? 0,
                LastModified = file.ModifiedTimeDateTimeOffset?.DateTime ?? DateTime.MinValue,
                ETag = file.Version?.ToString(),
                IsFolder = file.MimeType == "application/vnd.google-apps.folder"
            }).ToList();
        }
        catch
        {
            return Array.Empty<CloudFileInfo>();
        }
    }

    /// <inheritdoc />
    public async Task<CloudFileInfo?> GetFileInfoAsync(string remotePath)
    {
        if (_driveService == null || !_isAuthenticated)
            return null;

        try
        {
            var fileId = await GetFileIdAsync(remotePath);
            if (fileId == null)
                return null;

            var request = _driveService.Files.Get(fileId);
            request.Fields = "id, name, size, modifiedTime, mimeType, version";
            var file = await request.ExecuteAsync();

            return new CloudFileInfo
            {
                Name = file.Name,
                Path = remotePath,
                Size = file.Size ?? 0,
                LastModified = file.ModifiedTimeDateTimeOffset?.DateTime ?? DateTime.MinValue,
                ETag = file.Version?.ToString(),
                IsFolder = file.MimeType == "application/vnd.google-apps.folder"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> CreateFolderAsync(string remotePath)
    {
        if (_driveService == null || !_isAuthenticated || string.IsNullOrEmpty(_devStickyFolderId))
            return false;

        try
        {
            var folderId = await GetOrCreateFolderIdAsync(remotePath);
            return folderId != null;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _driveService?.Dispose();
        _driveService = null;
    }
}
