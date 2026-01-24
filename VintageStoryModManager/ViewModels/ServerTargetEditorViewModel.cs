using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     ViewModel for the server target editor dialog.
/// </summary>
public sealed partial class ServerTargetEditorViewModel : ObservableObject
{
    private readonly ServerTargetService _targetService;
    private readonly Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>> _testConnection;
    private readonly Func<string, HostKeyVerificationResult, Task<bool>> _hostKeyVerifier;
    private readonly string? _existingTargetId;
    private readonly string _stableTargetId;
    private string? _cachedHostKeyFingerprint;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private AuthenticationType _authType = AuthenticationType.Password;

    [ObservableProperty]
    private string? _privateKeyPath;

    [ObservableProperty]
    private string _remoteDataPath = string.Empty;

    [ObservableProperty]
    private bool _rememberCredentials;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    private bool _testSucceeded;

    [ObservableProperty]
    private string? _validationError;

    public ServerTargetEditorViewModel(
        ServerTargetService targetService,
        Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>> testConnection,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier,
        ServerTarget? existingTarget = null)
    {
        _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
        _testConnection = testConnection ?? throw new ArgumentNullException(nameof(testConnection));
        _hostKeyVerifier = hostKeyVerifier ?? throw new ArgumentNullException(nameof(hostKeyVerifier));

        if (existingTarget != null)
        {
            _existingTargetId = existingTarget.Id;
            _stableTargetId = existingTarget.Id;
            Name = existingTarget.Name;
            Host = existingTarget.Host;
            Port = existingTarget.Port;
            Username = existingTarget.Username;
            AuthType = existingTarget.AuthType;
            PrivateKeyPath = existingTarget.PrivateKeyPath;
            RemoteDataPath = existingTarget.RemoteDataPath;
            RememberCredentials = existingTarget.RememberCredentials;

            // Try to load existing password
            var existingPassword = _targetService.GetDecryptedPassword(existingTarget.Id);
            if (!string.IsNullOrEmpty(existingPassword))
            {
                Password = existingPassword;
            }
        }
        else
        {
            // For new targets, generate a stable ID to use across multiple test connections
            _stableTargetId = ServerTargetService.GenerateTargetId();
        }
    }

    /// <summary>
    ///     Whether this is editing an existing target (vs creating new).
    /// </summary>
    public bool IsEditing => _existingTargetId != null;

    /// <summary>
    ///     Dialog title.
    /// </summary>
    public string Title => IsEditing ? "Edit Server Target" : "Add Server Target";

    /// <summary>
    ///     Whether password authentication is selected.
    /// </summary>
    public bool IsPasswordAuth => AuthType == AuthenticationType.Password;

    /// <summary>
    ///     Whether private key authentication is selected.
    /// </summary>
    public bool IsPrivateKeyAuth => AuthType == AuthenticationType.PrivateKey;

    /// <summary>
    ///     Available authentication types.
    /// </summary>
    public IReadOnlyList<AuthenticationType> AuthTypes { get; } = new[] { AuthenticationType.Password, AuthenticationType.PrivateKey };

    partial void OnAuthTypeChanged(AuthenticationType value)
    {
        OnPropertyChanged(nameof(IsPasswordAuth));
        OnPropertyChanged(nameof(IsPrivateKeyAuth));
    }

    /// <summary>
    ///     Tests the connection to the server.
    /// </summary>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!Validate())
            return;

        IsTesting = true;
        TestResult = "Connecting...";
        TestSucceeded = false;

        try
        {
            var target = BuildTarget();
            var passwordToUse = string.IsNullOrEmpty(Password) ? null : Password;

            // Wrap the host key verifier to cache the fingerprint for new targets
            async Task<bool> wrappedVerifier(string fingerprint, HostKeyVerificationResult result)
            {
                var userTrusts = await _hostKeyVerifier(fingerprint, result).ConfigureAwait(false);

                // If user trusted and this is a new key, cache it
                if (userTrusts && result == HostKeyVerificationResult.NewKey)
                {
                    _cachedHostKeyFingerprint = fingerprint;
                }

                return userTrusts;
            }

            var success = await _testConnection(target, passwordToUse, wrappedVerifier).ConfigureAwait(false);

            TestSucceeded = success;
            TestResult = success ? "Connection successful!" : "Connection failed.";
        }
        catch (Exception ex)
        {
            TestSucceeded = false;
            TestResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    ///     Browses for a private key file.
    /// </summary>
    [RelayCommand]
    private void BrowsePrivateKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Private Key File",
            Filter = "All Files (*.*)|*.*|PEM Files (*.pem)|*.pem|PPK Files (*.ppk)|*.ppk",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            PrivateKeyPath = dialog.FileName;
        }
    }

    /// <summary>
    ///     Validates the input and returns whether it's valid.
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            ValidationError = "Host is required.";
            return false;
        }

        if (Port < 1 || Port > 65535)
        {
            ValidationError = "Port must be between 1 and 65535.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ValidationError = "Username is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RemoteDataPath))
        {
            ValidationError = "Remote data path is required.";
            return false;
        }

        if (AuthType == AuthenticationType.PrivateKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            ValidationError = "Private key path is required for key authentication.";
            return false;
        }

        if (AuthType == AuthenticationType.PrivateKey && !string.IsNullOrWhiteSpace(PrivateKeyPath) && !System.IO.File.Exists(PrivateKeyPath))
        {
            ValidationError = "Private key file does not exist.";
            return false;
        }

        ValidationError = null;
        return true;
    }

    /// <summary>
    ///     Saves the target configuration.
    /// </summary>
    /// <returns>True if saved successfully.</returns>
    public bool Save(out string? errorMessage)
    {
        if (!Validate())
        {
            errorMessage = ValidationError;
            return false;
        }

        var target = BuildTarget();
        var passwordToSave = RememberCredentials && !string.IsNullOrEmpty(Password) ? Password : null;

        if (IsEditing)
        {
            return _targetService.TryUpdateTarget(target, passwordToSave, out errorMessage);
        }
        else
        {
            return _targetService.TryAddTarget(target, passwordToSave, out errorMessage);
        }
    }

    /// <summary>
    ///     Gets the password to use for operations (only returns if user opted to remember).
    /// </summary>
    public string? GetPasswordForOperation()
    {
        return string.IsNullOrEmpty(Password) ? null : Password;
    }

    private ServerTarget BuildTarget()
    {
        // Get the known host key fingerprint from the database or cache
        string? knownHostKeyFingerprint = null;

        // First check the database (for existing targets)
        if (_existingTargetId != null)
        {
            var existingTarget = _targetService.GetTarget(_existingTargetId);
            knownHostKeyFingerprint = existingTarget?.KnownHostKeyFingerprint;
        }

        // If not found in database, use cached value (for new targets that were tested but not saved)
        if (knownHostKeyFingerprint == null && _cachedHostKeyFingerprint != null)
        {
            knownHostKeyFingerprint = _cachedHostKeyFingerprint;
        }

        return new ServerTarget
        {
            Id = _stableTargetId,
            Name = Name.Trim(),
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            AuthType = AuthType,
            PrivateKeyPath = IsPrivateKeyAuth ? PrivateKeyPath?.Trim() : null,
            RemoteDataPath = RemoteDataPath.Trim(),
            RememberCredentials = RememberCredentials,
            KnownHostKeyFingerprint = knownHostKeyFingerprint
        };
    }
}
