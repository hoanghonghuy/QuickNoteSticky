using System.IO;
using System.Text.Json;
using DevSticky.Interfaces;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DevSticky.Services;

/// <summary>
/// Cloud storage provider implementation for Microsoft OneDrive.
/// Uses Microsoft Graph SDK with OAuth 2.0 authentication.
/// </summary>
public class OneDriveStorageProvider : ICloudStorageProvider
{
    // Azure AD application registration details
    // Note: In production, these should be configured externally
    private const string ClientId = "YOUR_AZURE_APP_CLIENT_ID"; // Replace with actual client ID
    private const string TenantId = "consumers"; // Use "consumers" for personal Microsoft accounts
    
    private static readonly string[] Scopes = { "Files.ReadWrite.AppFolder", "User.Read", "offline_access" };
    
    private const string CredentialKey = "OneDrive_Token";
    private const string DevStickyFolder = "DevSticky";
    
    private IPublicClientApplication? _msalClient;
    private GraphServiceClient? _graphClient;
    private string? _driveId;
    private bool _isAuthenticated;
    private bool _disposed;

    /// <inheritdoc />
    public string ProviderName => "OneDrive";

    /// <inheritdoc />
    public bool IsAuthenticated => _isAuthenticated;

    public OneDriveStorageProvider()
    {
        InitializeMsalClient();
    }

    private void InitializeMsalClient()
    {
        _msalClient = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, TenantId)
            .WithRedirectUri("http://localhost")
            .Build();

        // Try to set up token cache persistence
        try
        {
            var cacheHelper = CreateCacheHelper().GetAwaiter().GetResult();
            cacheHelper.RegisterCache(_msalClient.UserTokenCache);
        }
        catch
        {
            // If cache setup fails, continue without persistence
        }
    }

    private static async Task<MsalCacheHelper> CreateCacheHelper()
    {
        var storageProperties = new StorageCreationPropertiesBuilder(
            "devsticky_msal_cache.txt",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevSticky"))
            .Build();

        return await MsalCacheHelper.CreateAsync(storageProperties);
    }

    /// <inheritdoc />
    public async Task<bool> AuthenticateAsync()
    {
        if (_msalClient == null)
            return false;

        try
        {
            AuthenticationResult? result = null;

            // Try to get token silently first (from cache)
            var accounts = await _msalClient.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            if (firstAccount != null)
            {
                try
                {
                    result = await _msalClient.AcquireTokenSilent(Scopes, firstAccount).ExecuteAsync();
                }
                catch (MsalUiRequiredException)
                {
                    // Token expired or not in cache, need interactive login
                }
            }

            // If silent acquisition failed, try interactive login
            if (result == null)
            {
                result = await _msalClient.AcquireTokenInteractive(Scopes)
                    .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                    .ExecuteAsync();
            }

            if (result != null && !string.IsNullOrEmpty(result.AccessToken))
            {
                // Create Graph client with the access token
                var authProvider = new BaseBearerTokenAuthenticationProvider(
                    new TokenProvider(result.AccessToken, _msalClient, Scopes));
                
                _graphClient = new GraphServiceClient(authProvider);
                _isAuthenticated = true;

                // Get the user's drive ID
                var drive = await _graphClient.Me.Drive.GetAsync();
                _driveId = drive?.Id;

                // Ensure DevSticky folder exists
                await EnsureDevStickyFolderExistsAsync();

                return true;
            }

            return false;
        }
        catch (Exception)
        {
            _isAuthenticated = false;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task SignOutAsync()
    {
        if (_msalClient != null)
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _msalClient.RemoveAsync(account);
            }
        }

        CredentialManagerService.DeleteCredential(CredentialKey);
        _graphClient = null;
        _driveId = null;
        _isAuthenticated = false;
    }

    private async Task EnsureDevStickyFolderExistsAsync()
    {
        if (_graphClient == null || _driveId == null) return;

        try
        {
            // Check if folder exists
            await _graphClient.Drives[_driveId].Root.ItemWithPath(DevStickyFolder).GetAsync();
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // Folder doesn't exist, create it
            var folder = new DriveItem
            {
                Name = DevStickyFolder,
                Folder = new Folder()
            };

            // Use Items endpoint to create folder in root
            var rootItem = await _graphClient.Drives[_driveId].Root.GetAsync();
            if (rootItem?.Id != null)
            {
                await _graphClient.Drives[_driveId].Items[rootItem.Id].Children.PostAsync(folder);
            }
        }
    }

    /// <inheritdoc />
    public async Task<string?> UploadFileAsync(string remotePath, byte[] content)
    {
        if (_graphClient == null || !_isAuthenticated || _driveId == null)
            return null;

        try
        {
            var fullPath = $"{DevStickyFolder}/{remotePath}";
            
            using var stream = new MemoryStream(content);
            
            // For files <= 4MB, use simple upload
            if (content.Length <= 4 * 1024 * 1024)
            {
                var driveItem = await _graphClient.Drives[_driveId].Root
                    .ItemWithPath(fullPath)
                    .Content
                    .PutAsync(stream);

                return driveItem?.ETag;
            }
            else
            {
                // For larger files, use upload session
                var uploadSession = await _graphClient.Drives[_driveId].Root
                    .ItemWithPath(fullPath)
                    .CreateUploadSession
                    .PostAsync(new CreateUploadSessionPostRequestBody());

                if (uploadSession?.UploadUrl == null)
                    return null;

                // Upload in chunks
                const int chunkSize = 320 * 1024; // 320 KB chunks
                var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, chunkSize);
                var uploadResult = await fileUploadTask.UploadAsync();

                return uploadResult.ItemResponse?.ETag;
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
        if (_graphClient == null || !_isAuthenticated || _driveId == null)
            return null;

        try
        {
            var fullPath = $"{DevStickyFolder}/{remotePath}";
            
            var stream = await _graphClient.Drives[_driveId].Root
                .ItemWithPath(fullPath)
                .Content
                .GetAsync();

            if (stream == null)
                return null;

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string remotePath)
    {
        if (_graphClient == null || !_isAuthenticated || _driveId == null)
            return false;

        try
        {
            var fullPath = $"{DevStickyFolder}/{remotePath}";
            
            await _graphClient.Drives[_driveId].Root
                .ItemWithPath(fullPath)
                .DeleteAsync();

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
        if (_graphClient == null || !_isAuthenticated || _driveId == null)
            return Array.Empty<CloudFileInfo>();

        try
        {
            var fullPath = string.IsNullOrEmpty(remotePath) 
                ? DevStickyFolder 
                : $"{DevStickyFolder}/{remotePath}";

            var children = await _graphClient.Drives[_driveId].Root
                .ItemWithPath(fullPath)
                .Children
                .GetAsync();

            if (children?.Value == null)
                return Array.Empty<CloudFileInfo>();

            return children.Value.Select(item => new CloudFileInfo
            {
                Name = item.Name ?? string.Empty,
                Path = item.ParentReference?.Path != null 
                    ? $"{item.ParentReference.Path}/{item.Name}".Replace($"/drive/root:/{DevStickyFolder}", "")
                    : item.Name ?? string.Empty,
                Size = item.Size ?? 0,
                LastModified = item.LastModifiedDateTime?.DateTime ?? DateTime.MinValue,
                ETag = item.ETag,
                IsFolder = item.Folder != null
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
        if (_graphClient == null || !_isAuthenticated || _driveId == null)
            return null;

        try
        {
            var fullPath = $"{DevStickyFolder}/{remotePath}";
            
            var item = await _graphClient.Drives[_driveId].Root
                .ItemWithPath(fullPath)
                .GetAsync();

            if (item == null)
                return null;

            return new CloudFileInfo
            {
                Name = item.Name ?? string.Empty,
                Path = remotePath,
                Size = item.Size ?? 0,
                LastModified = item.LastModifiedDateTime?.DateTime ?? DateTime.MinValue,
                ETag = item.ETag,
                IsFolder = item.Folder != null
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
        if (_graphClient == null || !_isAuthenticated || _driveId == null)
            return false;

        try
        {
            var fullPath = $"{DevStickyFolder}/{remotePath}";
            
            // Check if folder already exists
            try
            {
                var existing = await _graphClient.Drives[_driveId].Root
                    .ItemWithPath(fullPath)
                    .GetAsync();
                
                if (existing?.Folder != null)
                    return true; // Folder already exists
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // Folder doesn't exist, continue to create
            }

            // Get parent path and folder name
            var lastSlash = remotePath.LastIndexOf('/');
            var parentPath = lastSlash > 0 ? remotePath[..lastSlash] : "";
            var folderName = lastSlash > 0 ? remotePath[(lastSlash + 1)..] : remotePath;

            var parentFullPath = string.IsNullOrEmpty(parentPath) 
                ? DevStickyFolder 
                : $"{DevStickyFolder}/{parentPath}";

            var folder = new DriveItem
            {
                Name = folderName,
                Folder = new Folder()
            };

            // Get parent item ID and create folder
            var parentItem = await _graphClient.Drives[_driveId].Root
                .ItemWithPath(parentFullPath)
                .GetAsync();

            if (parentItem?.Id != null)
            {
                await _graphClient.Drives[_driveId].Items[parentItem.Id].Children.PostAsync(folder);
            }

            return true;
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
        _graphClient = null;
        _msalClient = null;
    }
}
