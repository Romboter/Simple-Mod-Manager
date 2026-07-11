using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager;

internal sealed class ModUsagePromptData
{
    public ModUsagePromptData(
        IReadOnlyList<ModUsageVoteCandidateViewModel> candidates,
        IReadOnlyList<ModUsageTrackingKey> candidateKeys,
        int skippedCount)
    {
        Candidates = candidates ?? Array.Empty<ModUsageVoteCandidateViewModel>();
        CandidateKeys = candidateKeys ?? Array.Empty<ModUsageTrackingKey>();
        SkippedCount = skippedCount < 0 ? 0 : skippedCount;
    }

    public IReadOnlyList<ModUsageVoteCandidateViewModel> Candidates { get; }

    public IReadOnlyList<ModUsageTrackingKey> CandidateKeys { get; }

    public int SkippedCount { get; }
}
