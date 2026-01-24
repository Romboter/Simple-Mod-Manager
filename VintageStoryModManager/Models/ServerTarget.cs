namespace VintageStoryModManager.Models;

/// <summary>
///     Authentication method for SFTP connection.
/// </summary>
public enum AuthenticationType
{
    Password = 0,
    PrivateKey = 1
}

/// <summary>
///     Represents a remote server target for mod synchronization via SFTP.
/// </summary>
public sealed class ServerTarget
{
    /// <summary>
    ///     Unique identifier for this server target.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     User-friendly display name for this target.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     SSH/SFTP host address.
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    ///     SSH/SFTP port number (default 22).
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    ///     Username for authentication.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    ///     Authentication method (password or private key).
    /// </summary>
    public AuthenticationType AuthType { get; set; } = AuthenticationType.Password;

    /// <summary>
    ///     Path to private key file (when AuthType is PrivateKey).
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    ///     Remote path to the Vintage Story data directory.
    ///     Mods will be synced to {RemoteDataPath}/Mods.
    /// </summary>
    public required string RemoteDataPath { get; set; }

    /// <summary>
    ///     Stored host key fingerprint for TOFU (Trust On First Use) verification.
    /// </summary>
    public string? KnownHostKeyFingerprint { get; set; }

    /// <summary>
    ///     Whether to persist credentials (password or key passphrase) locally.
    /// </summary>
    public bool RememberCredentials { get; set; }

    /// <summary>
    ///     Last successful connection time.
    /// </summary>
    public DateTime? LastConnected { get; set; }
}
