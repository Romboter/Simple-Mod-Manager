using System.Globalization;
using System.Text;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal static class ModCompatibilityReviewHelper
{
    internal static string BuildExperimentalCompReviewMessage(
            ModCompatibilityCommentsService.ExperimentalCompReviewResult result)
        {
            if (result.Top3 is not { Count: > 0 }) return result.Reason ?? string.Empty;

            var builder = new StringBuilder();
            for (var index = 0; index < result.Top3.Count; index++)
            {
                var comment = result.Top3[index];
                var totalScore = comment.ScoreBreakdown?.Values.Sum() ?? 0;
                var scoreText = FormatExperimentalCompReviewScore(totalScore);

                builder.Append(index + 1);
                builder.Append(". [");
                builder.Append(scoreText);
                builder.Append("] ");
                builder.AppendLine(comment.Snippet);
            }

            return builder.ToString().TrimEnd();
        }

    private static string FormatExperimentalCompReviewScore(double score)
        {
            var rounded = Math.Round(score, 2);
            return rounded.ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture);
        }

    internal static string ResolveExperimentalCompReviewIdentifier(ModListItemViewModel selectedMod)
        {
            var fromUrl = ModDatabaseSlugParser.TryExtractModSlug(selectedMod.ModDatabasePageUrl);
            if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl!;

            if (!string.IsNullOrWhiteSpace(selectedMod.ModDatabaseAssetId)) return selectedMod.ModDatabaseAssetId!;

            return selectedMod.ModId;
        }
}
