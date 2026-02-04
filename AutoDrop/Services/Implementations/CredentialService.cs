using System.Security.Cryptography;
using System.Text;
using AutoDrop.Core.Constants;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Secure credential storage service using Windows DPAPI for encryption at rest.
/// Credentials are encrypted per-user and stored in the app data folder.
/// </summary>
public sealed class CredentialService : ICredentialService
{
    private readonly ILogger<CredentialService> _logger;
    private readonly string _credentialsFolder;
    private static readonly byte[] EntropyBytes = "AutoDropCredentials2026"u8.ToArray();

    public CredentialService(ILogger<CredentialService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.AppDataFolderName,
            "Credentials");
        
        EnsureCredentialsFolderExists();
        _logger.LogDebug("CredentialService initialized. Folder: {Folder}", _credentialsFolder);
    }

    private void EnsureCredentialsFolderExists()
    {
        if (!Directory.Exists(_credentialsFolder))
        {
            Directory.CreateDirectory(_credentialsFolder);
            // Set folder as hidden for additional privacy
            var dirInfo = new DirectoryInfo(_credentialsFolder);
            dirInfo.Attributes |= FileAttributes.Hidden;
        }
    }

    private string GetCredentialPath(string key)
    {
        // Sanitize key to be a valid filename
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_credentialsFolder, $"{safeKey}.enc");
    }

    public async Task<bool> StoreCredentialAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectedData.Protect(plainBytes, EntropyBytes, DataProtectionScope.CurrentUser);
            
            var filePath = GetCredentialPath(key);
            
            // Delete existing file first to avoid access issues with hidden files
            if (File.Exists(filePath))
            {
                try
                {
                    // Remove hidden attribute before deleting
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove existing credential file, attempting overwrite");
                }
            }
            
            await File.WriteAllBytesAsync(filePath, encryptedBytes);
            
            // Set file as hidden for additional privacy
            File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);
            
            _logger.LogDebug("Stored credential: {Key}", key);
            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to encrypt credential: {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store credential: {Key}", key);
            return false;
        }
    }

    public async Task<string?> GetCredentialAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var filePath = GetCredentialPath(key);
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("Credential not found: {Key}", key);
            return null;
        }

        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(filePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, EntropyBytes, DataProtectionScope.CurrentUser);
            
            _logger.LogDebug("Retrieved credential: {Key}", key);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt credential: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve credential: {Key}", key);
            return null;
        }
    }

    public Task<bool> RemoveCredentialAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var filePath = GetCredentialPath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Removed credential: {Key}", key);
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove credential: {Key}", key);
            return Task.FromResult(false);
        }
    }

    public Task<bool> HasCredentialAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var filePath = GetCredentialPath(key);
        return Task.FromResult(File.Exists(filePath));
    }
}
