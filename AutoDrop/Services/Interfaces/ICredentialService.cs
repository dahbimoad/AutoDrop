namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service for securely storing and retrieving sensitive credentials.
/// Uses Windows DPAPI for encryption at rest.
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Stores a credential securely using DPAPI encryption.
    /// </summary>
    /// <param name="key">Unique identifier for the credential (e.g., "OpenAI_ApiKey").</param>
    /// <param name="value">The sensitive value to store.</param>
    /// <returns>True if stored successfully.</returns>
    Task<bool> StoreCredentialAsync(string key, string value);
    
    /// <summary>
    /// Retrieves a previously stored credential.
    /// </summary>
    /// <param name="key">Unique identifier for the credential.</param>
    /// <returns>The decrypted credential value, or null if not found.</returns>
    Task<string?> GetCredentialAsync(string key);
    
    /// <summary>
    /// Removes a stored credential.
    /// </summary>
    /// <param name="key">Unique identifier for the credential.</param>
    /// <returns>True if removed successfully.</returns>
    Task<bool> RemoveCredentialAsync(string key);
    
    /// <summary>
    /// Checks if a credential exists.
    /// </summary>
    /// <param name="key">Unique identifier for the credential.</param>
    /// <returns>True if the credential exists.</returns>
    Task<bool> HasCredentialAsync(string key);
}
