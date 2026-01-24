using System.Security.Cryptography;
using System.Text;

namespace VintageStoryModManager.Models;

/// <summary>
///     Manages DPAPI-encrypted credential storage for SFTP connections.
/// </summary>
public sealed class SftpCredential
{
    /// <summary>
    ///     DPAPI-encrypted secret (password or key passphrase) as base64.
    /// </summary>
    public string? EncryptedSecret { get; set; }

    /// <summary>
    ///     Creates an encrypted credential from plaintext.
    /// </summary>
    /// <param name="plainText">The plaintext password or passphrase.</param>
    /// <returns>A new SftpCredential with the encrypted secret.</returns>
    public static SftpCredential Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return new SftpCredential();

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return new SftpCredential { EncryptedSecret = Convert.ToBase64String(encrypted) };
    }

    /// <summary>
    ///     Decrypts the stored secret.
    /// </summary>
    /// <returns>The plaintext secret, or null if no secret is stored.</returns>
    public string? Decrypt()
    {
        if (string.IsNullOrEmpty(EncryptedSecret))
            return null;

        try
        {
            var encrypted = Convert.FromBase64String(EncryptedSecret);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // Decryption failed (e.g., credential from different user or machine)
            return null;
        }
    }

    /// <summary>
    ///     Checks if this credential has a stored secret.
    /// </summary>
    public bool HasSecret => !string.IsNullOrEmpty(EncryptedSecret);
}
