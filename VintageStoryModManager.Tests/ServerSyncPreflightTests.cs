using System.Windows;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ServerSyncPreflightTests
{
    [Fact]
    public void Evaluate_NonServerProfile_BlocksWithInformation_WithoutLookingUpTarget()
    {
        var lookupCalled = false;

        var result = ServerSyncPreflight.Evaluate(
            false, "target-1", _ => { lookupCalled = true; return null; }, @"C:\data");

        Assert.Null(result.Target);
        Assert.Equal(
            "This feature is only available for Server profiles.\n\nTo use this feature, create a new profile and set its type to 'Server'.",
            result.ErrorMessage);
        Assert.Equal(MessageBoxImage.Information, result.Icon);
        Assert.False(lookupCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Evaluate_MissingTargetId_BlocksWithWarning(string? targetId)
    {
        var result = ServerSyncPreflight.Evaluate(true, targetId, _ => null, @"C:\data");

        Assert.Null(result.Target);
        Assert.Equal("No server target is configured for this profile.", result.ErrorMessage);
        Assert.Equal(MessageBoxImage.Warning, result.Icon);
    }

    [Fact]
    public void Evaluate_TargetNotFound_BlocksWithError()
    {
        var result = ServerSyncPreflight.Evaluate(true, "target-1", _ => null, @"C:\data");

        Assert.Null(result.Target);
        Assert.Equal("The configured server target was not found. It may have been deleted.", result.ErrorMessage);
        Assert.Equal(MessageBoxImage.Error, result.Icon);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_BlankDataDirectory_BlocksWithWarning(string? dataDirectory)
    {
        var target = TestData.CreateServerTarget();

        var result = ServerSyncPreflight.Evaluate(true, target.Id, _ => target, dataDirectory);

        Assert.Null(result.Target);
        Assert.Equal("No data directory is configured for this profile.", result.ErrorMessage);
        Assert.Equal(MessageBoxImage.Warning, result.Icon);
    }

    [Fact]
    public void Evaluate_AllPreconditionsMet_ReturnsTarget()
    {
        var target = TestData.CreateServerTarget();
        string? requestedId = null;

        var result = ServerSyncPreflight.Evaluate(
            true, target.Id, id => { requestedId = id; return target; }, @"C:\data");

        Assert.Same(target, result.Target);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(target.Id, requestedId);
    }

    [Theory]
    [InlineData(true, "target-1", true)]
    [InlineData(true, null, false)]
    [InlineData(true, "", false)]
    [InlineData(false, "target-1", false)]
    [InlineData(false, null, false)]
    public void CanSyncToServer_RequiresServerProfileAndTargetId(bool isServerProfile, string? targetId, bool expected)
    {
        Assert.Equal(expected, ServerSyncPreflight.CanSyncToServer(isServerProfile, targetId));
    }
}
