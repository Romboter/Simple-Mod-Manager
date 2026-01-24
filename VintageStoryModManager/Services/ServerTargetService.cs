using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Manages server targets for SFTP synchronization, including CRUD operations
///     and secure credential storage using DPAPI encryption.
/// </summary>
public sealed class ServerTargetService
{
    private const string TargetsFileName = "ServerTargets.json";
    private const string CredentialsFileName = "ServerCredentials.dat";

    private readonly string _credentialsPath;
    private readonly object _syncRoot = new();
    private readonly string _targetsPath;
    private readonly string _targetsTempPath;
    private readonly string _credentialsTempPath;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private Dictionary<string, ServerTarget> _targets = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SftpCredential> _credentials = new(StringComparer.OrdinalIgnoreCase);

    public ServerTargetService(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(configDirectory));

        ConfigDirectory = Path.GetFullPath(configDirectory);
        Directory.CreateDirectory(ConfigDirectory);

        _targetsPath = Path.Combine(ConfigDirectory, TargetsFileName);
        _targetsTempPath = Path.Combine(ConfigDirectory, TargetsFileName + ".tmp");
        _credentialsPath = Path.Combine(ConfigDirectory, CredentialsFileName);
        _credentialsTempPath = Path.Combine(ConfigDirectory, CredentialsFileName + ".tmp");

        Load();
    }

    public string ConfigDirectory { get; }

    /// <summary>
    ///     Gets all configured server targets.
    /// </summary>
    public IReadOnlyList<ServerTarget> GetAllTargets()
    {
        lock (_syncRoot)
        {
            return _targets.Values.ToList();
        }
    }

    /// <summary>
    ///     Gets a server target by ID.
    /// </summary>
    public ServerTarget? GetTarget(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_syncRoot)
        {
            return _targets.GetValueOrDefault(id);
        }
    }

    /// <summary>
    ///     Gets a server target by name.
    /// </summary>
    public ServerTarget? GetTargetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_syncRoot)
        {
            return _targets.Values.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Adds a new server target.
    /// </summary>
    /// <param name="target">The target configuration.</param>
    /// <param name="password">Optional password or passphrase to store (if RememberCredentials is true).</param>
    /// <param name="error">Error message if operation fails.</param>
    /// <returns>True if successful.</returns>
    public bool TryAddTarget(ServerTarget target, string? password, out string? error)
    {
        if (target is null)
        {
            error = "Target cannot be null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Id))
        {
            error = "Target ID cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Name))
        {
            error = "Target name cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Host))
        {
            error = "Host cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Username))
        {
            error = "Username cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.RemoteDataPath))
        {
            error = "Remote data path cannot be empty.";
            return false;
        }

        lock (_syncRoot)
        {
            if (_targets.ContainsKey(target.Id))
            {
                error = "A target with this ID already exists.";
                return false;
            }

            if (_targets.Values.Any(t => string.Equals(t.Name, target.Name, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A target with this name already exists.";
                return false;
            }

            _targets[target.Id] = target;

            if (target.RememberCredentials && !string.IsNullOrEmpty(password))
            {
                _credentials[target.Id] = SftpCredential.Encrypt(password);
            }

            try
            {
                PersistTargets();
                PersistCredentials();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                _targets.Remove(target.Id);
                _credentials.Remove(target.Id);
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    ///     Updates an existing server target.
    /// </summary>
    /// <param name="target">The updated target configuration.</param>
    /// <param name="newPassword">New password to store (null to keep existing, empty to clear).</param>
    /// <param name="error">Error message if operation fails.</param>
    /// <returns>True if successful.</returns>
    public bool TryUpdateTarget(ServerTarget target, string? newPassword, out string? error)
    {
        if (target is null)
        {
            error = "Target cannot be null.";
            return false;
        }

        lock (_syncRoot)
        {
            if (!_targets.ContainsKey(target.Id))
            {
                error = "Target not found.";
                return false;
            }

            // Check for name collision with other targets
            var existingWithName = _targets.Values.FirstOrDefault(t =>
                !string.Equals(t.Id, target.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Name, target.Name, StringComparison.OrdinalIgnoreCase));

            if (existingWithName != null)
            {
                error = "A target with this name already exists.";
                return false;
            }

            var previousTarget = _targets[target.Id];
            _targets[target.Id] = target;

            // Handle credential update
            if (newPassword != null)
            {
                if (target.RememberCredentials && !string.IsNullOrEmpty(newPassword))
                {
                    _credentials[target.Id] = SftpCredential.Encrypt(newPassword);
                }
                else
                {
                    _credentials.Remove(target.Id);
                }
            }
            else if (!target.RememberCredentials)
            {
                // RememberCredentials turned off, clear stored credential
                _credentials.Remove(target.Id);
            }

            try
            {
                PersistTargets();
                PersistCredentials();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                _targets[target.Id] = previousTarget;
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    ///     Deletes a server target.
    /// </summary>
    public bool TryDeleteTarget(string id, out string? error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Target ID cannot be empty.";
            return false;
        }

        lock (_syncRoot)
        {
            if (!_targets.Remove(id, out var removedTarget))
            {
                error = "Target not found.";
                return false;
            }

            _credentials.Remove(id);

            try
            {
                PersistTargets();
                PersistCredentials();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                _targets[id] = removedTarget;
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    ///     Gets the decrypted password for a target.
    /// </summary>
    public string? GetDecryptedPassword(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return null;

        lock (_syncRoot)
        {
            if (_credentials.TryGetValue(targetId, out var credential))
            {
                return credential.Decrypt();
            }
            return null;
        }
    }

    /// <summary>
    ///     Sets or clears the password for a target.
    /// </summary>
    public bool TrySetPassword(string targetId, string? password, out string? error)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            error = "Target ID cannot be empty.";
            return false;
        }

        lock (_syncRoot)
        {
            if (!_targets.TryGetValue(targetId, out var target))
            {
                error = "Target not found.";
                return false;
            }

            if (!string.IsNullOrEmpty(password))
            {
                _credentials[targetId] = SftpCredential.Encrypt(password);
            }
            else
            {
                _credentials.Remove(targetId);
            }

            try
            {
                PersistCredentials();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    ///     Updates the last connected timestamp for a target.
    /// </summary>
    public void UpdateLastConnected(string targetId)
    {
        lock (_syncRoot)
        {
            if (_targets.TryGetValue(targetId, out var target))
            {
                target.LastConnected = DateTime.UtcNow;
                try
                {
                    PersistTargets();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to persist last connected time: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    ///     Updates the known host key fingerprint for a target (TOFU).
    /// </summary>
    public bool TryUpdateHostKeyFingerprint(string targetId, string fingerprint, out string? error)
    {
        lock (_syncRoot)
        {
            if (!_targets.TryGetValue(targetId, out var target))
            {
                error = "Target not found.";
                return false;
            }

            target.KnownHostKeyFingerprint = fingerprint;

            try
            {
                PersistTargets();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    ///     Creates a new unique ID for a server target.
    /// </summary>
    public static string GenerateTargetId()
    {
        return Guid.NewGuid().ToString("N")[..12];
    }

    private void Load()
    {
        lock (_syncRoot)
        {
            LoadTargets();
            LoadCredentials();
        }
    }

    private void LoadTargets()
    {
        _targets.Clear();

        if (!File.Exists(_targetsPath))
            return;

        try
        {
            var json = File.ReadAllText(_targetsPath);
            var root = JsonNode.Parse(json)?.AsObject();

            if (root?["targets"] is JsonArray targetsArray)
            {
                foreach (var item in targetsArray)
                {
                    if (item is not JsonObject targetObj)
                        continue;

                    var id = targetObj["id"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    var target = new ServerTarget
                    {
                        Id = id,
                        Name = targetObj["name"]?.GetValue<string>() ?? id,
                        Host = targetObj["host"]?.GetValue<string>() ?? string.Empty,
                        Port = targetObj["port"]?.GetValue<int>() ?? 22,
                        Username = targetObj["username"]?.GetValue<string>() ?? string.Empty,
                        AuthType = Enum.TryParse<AuthenticationType>(targetObj["authType"]?.GetValue<string>(), true, out var authType)
                            ? authType
                            : AuthenticationType.Password,
                        PrivateKeyPath = targetObj["privateKeyPath"]?.GetValue<string>(),
                        RemoteDataPath = targetObj["remoteDataPath"]?.GetValue<string>() ?? string.Empty,
                        KnownHostKeyFingerprint = targetObj["knownHostKeyFingerprint"]?.GetValue<string>(),
                        RememberCredentials = targetObj["rememberCredentials"]?.GetValue<bool>() ?? false,
                        LastConnected = targetObj["lastConnected"]?.GetValue<DateTime?>()
                    };

                    _targets[id] = target;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load server targets: {ex.Message}");
        }
    }

    private void LoadCredentials()
    {
        _credentials.Clear();

        if (!File.Exists(_credentialsPath))
            return;

        try
        {
            var json = File.ReadAllText(_credentialsPath);
            var root = JsonNode.Parse(json)?.AsObject();

            if (root?["credentials"] is JsonObject credentialsObj)
            {
                foreach (var kvp in credentialsObj)
                {
                    var targetId = kvp.Key;
                    var encryptedSecret = kvp.Value?.GetValue<string>();

                    if (!string.IsNullOrEmpty(targetId) && !string.IsNullOrEmpty(encryptedSecret))
                    {
                        _credentials[targetId] = new SftpCredential { EncryptedSecret = encryptedSecret };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load server credentials: {ex.Message}");
        }
    }

    private void PersistTargets()
    {
        var targetsArray = new JsonArray();

        foreach (var target in _targets.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var targetObj = new JsonObject
            {
                ["id"] = target.Id,
                ["name"] = target.Name,
                ["host"] = target.Host,
                ["port"] = target.Port,
                ["username"] = target.Username,
                ["authType"] = target.AuthType.ToString(),
                ["privateKeyPath"] = target.PrivateKeyPath,
                ["remoteDataPath"] = target.RemoteDataPath,
                ["knownHostKeyFingerprint"] = target.KnownHostKeyFingerprint,
                ["rememberCredentials"] = target.RememberCredentials,
                ["lastConnected"] = target.LastConnected
            };

            targetsArray.Add(targetObj);
        }

        var root = new JsonObject
        {
            ["version"] = 1,
            ["targets"] = targetsArray
        };

        var json = root.ToJsonString(_serializerOptions);

        File.WriteAllText(_targetsTempPath, json);
        if (File.Exists(_targetsPath))
            File.Replace(_targetsTempPath, _targetsPath, null);
        else
            File.Move(_targetsTempPath, _targetsPath);
    }

    private void PersistCredentials()
    {
        var credentialsObj = new JsonObject();

        foreach (var kvp in _credentials)
        {
            if (!string.IsNullOrEmpty(kvp.Value.EncryptedSecret))
            {
                credentialsObj[kvp.Key] = kvp.Value.EncryptedSecret;
            }
        }

        var root = new JsonObject
        {
            ["version"] = 1,
            ["credentials"] = credentialsObj
        };

        var json = root.ToJsonString(_serializerOptions);

        File.WriteAllText(_credentialsTempPath, json);
        if (File.Exists(_credentialsPath))
            File.Replace(_credentialsTempPath, _credentialsPath, null);
        else
            File.Move(_credentialsTempPath, _credentialsPath);
    }
}
