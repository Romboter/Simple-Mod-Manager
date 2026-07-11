using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ConfirmationServiceInterfaceTests
{
    private sealed class RecordingConfirmation : IConfirmationService
    {
        public bool ConfirmAnswer { get; set; }
        public ThreeWayConfirmResult AnswerToReturn { get; set; } = ThreeWayConfirmResult.Yes;
        public bool OkCancelAnswer { get; set; } = true;
        public bool SuppressCallbackInvoked { get; private set; }
        public string? LastYesText { get; private set; }
        public string? LastNoText { get; private set; }
        public string? LastOkText { get; private set; }
        public string? LastCancelText { get; private set; }

        public Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null) =>
            Task.FromResult(ConfirmAnswer);

        public Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information) =>
            Task.CompletedTask;

        public Task<ThreeWayConfirmResult> ConfirmThreeWayAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? yesText = null, string? noText = null,
            SuppressibleConfirmOption? suppressOption = null)
        {
            LastYesText = yesText;
            LastNoText = noText;
            return Task.FromResult(AnswerToReturn);
        }

        public Task<bool> ConfirmOkCancelAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? okText = null, string? cancelText = null)
        {
            LastOkText = okText;
            LastCancelText = cancelText;
            return Task.FromResult(OkCancelAnswer);
        }
    }

    [Fact]
    public async Task ConfirmThreeWayAsync_ReturnsConfiguredAnswer()
    {
        IConfirmationService service = new RecordingConfirmation { AnswerToReturn = ThreeWayConfirmResult.Cancel };

        var result = await service.ConfirmThreeWayAsync("message", "title");

        Assert.Equal(ThreeWayConfirmResult.Cancel, result);
    }

    [Fact]
    public async Task ConfirmThreeWayAsync_PassesCustomButtonText()
    {
        var recording = new RecordingConfirmation();
        IConfirmationService service = recording;

        await service.ConfirmThreeWayAsync("message", "title", yesText: "Only Modlist mods", noText: "Add Modlist mods");

        Assert.Equal("Only Modlist mods", recording.LastYesText);
        Assert.Equal("Add Modlist mods", recording.LastNoText);
    }

    [Fact]
    public void SuppressibleConfirmOption_InvokesCallback()
    {
        var invoked = false;
        var option = new SuppressibleConfirmOption("No, don't ask again", () => invoked = true);

        option.OnSelected();

        Assert.True(invoked);
        Assert.Equal("No, don't ask again", option.ButtonText);
    }

    [Fact]
    public async Task ConfirmOkCancelAsync_ReturnsTrueForOk()
    {
        var recording = new RecordingConfirmation { OkCancelAnswer = true };
        IConfirmationService service = recording;

        var result = await service.ConfirmOkCancelAsync("message", "title");

        Assert.True(result);
    }

    [Fact]
    public async Task ConfirmOkCancelAsync_PassesCustomButtonText()
    {
        var recording = new RecordingConfirmation();
        IConfirmationService service = recording;

        await service.ConfirmOkCancelAsync("message", "title", cancelText: "No thanks");

        Assert.Equal("No thanks", recording.LastCancelText);
    }
}
