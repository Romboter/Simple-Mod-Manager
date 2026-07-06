using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ServerConnectionHelperTests : IDisposable
{
    private readonly string _configDir = Directory.CreateTempSubdirectory("smm-srv-").FullName;
    private readonly ServerTargetService _service;

    public ServerConnectionHelperTests()
    {
        _service = new ServerTargetService(_configDir);
    }

    public void Dispose()
    {
        Directory.Delete(_configDir, true);
    }

    [Fact]
    public async Task Verifier_TrustedNewKey_StoresFingerprintAndReturnsTrue()
    {
        var target = TestData.CreateServerTarget();
        Assert.True(_service.TryAddTarget(target, null, out _));

        var verifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _service, target, (_, _) => Task.FromResult(true));

        var trusted = await verifier("SHA256:abc123", HostKeyVerificationResult.NewKey);

        Assert.True(trusted);
        Assert.Equal("SHA256:abc123", _service.GetTarget(target.Id)!.KnownHostKeyFingerprint);
    }

    [Fact]
    public async Task Verifier_NotTrusted_DoesNotStoreAndReturnsFalse()
    {
        var target = TestData.CreateServerTarget();
        Assert.True(_service.TryAddTarget(target, null, out _));

        var verifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _service, target, (_, _) => Task.FromResult(false));

        var trusted = await verifier("SHA256:abc123", HostKeyVerificationResult.NewKey);

        Assert.False(trusted);
        Assert.Null(_service.GetTarget(target.Id)!.KnownHostKeyFingerprint);
    }

    [Theory]
    [InlineData(HostKeyVerificationResult.Trusted)]
    [InlineData(HostKeyVerificationResult.Mismatch)]
    public async Task Verifier_TrustedButNotNewKey_DoesNotStore(HostKeyVerificationResult result)
    {
        var target = TestData.CreateServerTarget();
        Assert.True(_service.TryAddTarget(target, null, out _));

        var verifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _service, target, (_, _) => Task.FromResult(true));

        var trusted = await verifier("SHA256:abc123", result);

        Assert.True(trusted);
        Assert.Null(_service.GetTarget(target.Id)!.KnownHostKeyFingerprint);
    }

    [Fact]
    public async Task Verifier_UnknownTarget_StillReturnsBaseResultWithoutThrowing()
    {
        // Target never added to the service - TryUpdateHostKeyFingerprint fails internally.
        var target = TestData.CreateServerTarget("never-added");

        var verifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _service, target, (_, _) => Task.FromResult(true));

        var trusted = await verifier("SHA256:abc123", HostKeyVerificationResult.NewKey);

        Assert.True(trusted);
    }

    [Fact]
    public async Task Verifier_PassesFingerprintAndResultToBaseVerifier()
    {
        var target = TestData.CreateServerTarget();
        string? seenFingerprint = null;
        HostKeyVerificationResult? seenResult = null;

        var verifier = ServerConnectionHelper.CreateHostKeyVerifierWithStorage(
            _service, target, (fingerprint, result) =>
            {
                seenFingerprint = fingerprint;
                seenResult = result;
                return Task.FromResult(false);
            });

        await verifier("SHA256:xyz", HostKeyVerificationResult.Mismatch);

        Assert.Equal("SHA256:xyz", seenFingerprint);
        Assert.Equal(HostKeyVerificationResult.Mismatch, seenResult);
    }
}
