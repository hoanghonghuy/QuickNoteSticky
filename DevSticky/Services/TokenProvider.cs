using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DevSticky.Services;

/// <summary>
/// Token provider for Microsoft Graph authentication.
/// Handles token acquisition and refresh using MSAL.
/// </summary>
internal class TokenProvider : IAccessTokenProvider
{
    private string _accessToken;
    private readonly IPublicClientApplication _msalClient;
    private readonly string[] _scopes;

    public TokenProvider(string accessToken, IPublicClientApplication msalClient, string[] scopes)
    {
        _accessToken = accessToken;
        _msalClient = msalClient;
        _scopes = scopes;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator(
        new[] { "graph.microsoft.com" });

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri, 
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Try to get a fresh token silently
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            if (firstAccount != null)
            {
                var result = await _msalClient
                    .AcquireTokenSilent(_scopes, firstAccount)
                    .ExecuteAsync(cancellationToken);

                if (!string.IsNullOrEmpty(result.AccessToken))
                {
                    _accessToken = result.AccessToken;
                }
            }
        }
        catch
        {
            // If silent acquisition fails, use the existing token
        }

        return _accessToken;
    }
}
