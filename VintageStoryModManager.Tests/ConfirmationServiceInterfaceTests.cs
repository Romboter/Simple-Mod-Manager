using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ConfirmationServiceInterfaceTests
{
    private sealed class RecordingConfirmation : IConfirmationService
    {
        public bool ConfirmAnswer { get; set; }
        public bool OkCancelAnswer { get; set; } = true;
        public string? LastOkText { get; private set; }
        public string? LastCancelText { get; private set; }

        public Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null)
        {
            return Task.FromResult(ConfirmAnswer);
        }

        public Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information)
        {
            return Task.CompletedTask;
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
