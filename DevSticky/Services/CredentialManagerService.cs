using System.Runtime.InteropServices;
using System.Text;

namespace DevSticky.Services;

/// <summary>
/// Service for storing and retrieving credentials from Windows Credential Manager.
/// Used to securely store OAuth tokens for cloud providers.
/// </summary>
public static class CredentialManagerService
{
    private const string CredentialPrefix = "DevSticky_";

    #region Win32 API

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWriteW(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);

    private const uint CRED_TYPE_GENERIC = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    #endregion

    /// <summary>
    /// Saves a credential to Windows Credential Manager.
    /// </summary>
    /// <param name="key">The key/name for the credential.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool SaveCredential(string key, string value)
    {
        try
        {
            var targetName = CredentialPrefix + key;
            var credentialBytes = Encoding.UTF8.GetBytes(value);
            var credentialBlobPtr = Marshal.AllocHGlobal(credentialBytes.Length);

            try
            {
                Marshal.Copy(credentialBytes, 0, credentialBlobPtr, credentialBytes.Length);

                var credential = new CREDENTIAL
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = targetName,
                    CredentialBlobSize = (uint)credentialBytes.Length,
                    CredentialBlob = credentialBlobPtr,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    UserName = Environment.UserName
                };

                return CredWriteW(ref credential, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(credentialBlobPtr);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Retrieves a credential from Windows Credential Manager.
    /// </summary>
    /// <param name="key">The key/name of the credential.</param>
    /// <returns>The stored value, or null if not found.</returns>
    public static string? GetCredential(string key)
    {
        try
        {
            var targetName = CredentialPrefix + key;

            if (!CredReadW(targetName, CRED_TYPE_GENERIC, 0, out var credentialPtr))
                return null;

            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
                
                if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                    return null;

                var credentialBytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, credentialBytes, 0, (int)credential.CredentialBlobSize);
                
                return Encoding.UTF8.GetString(credentialBytes);
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes a credential from Windows Credential Manager.
    /// </summary>
    /// <param name="key">The key/name of the credential to delete.</param>
    /// <returns>True if successful or credential didn't exist, false otherwise.</returns>
    public static bool DeleteCredential(string key)
    {
        try
        {
            var targetName = CredentialPrefix + key;
            CredDeleteW(targetName, CRED_TYPE_GENERIC, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
