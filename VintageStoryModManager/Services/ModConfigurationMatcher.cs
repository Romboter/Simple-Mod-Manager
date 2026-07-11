using System.IO;
using System.Text;

namespace VintageStoryModManager.Services;

internal static class ModConfigurationMatcher
{
    private static readonly int AutomaticConfigMaxWordDistance =
        DevConfig.AutomaticConfigMaxWordDistance;

    internal static List<(string ModId, string ConfigPath)> FindConfigMatches(
            IReadOnlyList<(string ModId, string DisplayName)> mods,
            IReadOnlyList<string> configPaths)
        {
            var results = new List<(string ModId, string ConfigPath)>();
            if (mods.Count == 0 || configPaths.Count == 0) return results;

            var candidates = configPaths
                .Select(path => (Path: path,
                    Tokens: BuildSearchTokens(Path.GetFileNameWithoutExtension(path) ?? string.Empty)))
                .Where(candidate => candidate.Tokens.Count > 0)
                .ToList();

            if (candidates.Count == 0) return results;

            foreach (var mod in mods)
            {
                var tokenSets = BuildModSearchTokenSets(mod.ModId, mod.DisplayName);
                if (tokenSets.Count == 0) continue;

                string? bestPath = null;
                var bestScore = int.MaxValue;
                var bestCandidateIndex = -1;
                var bestWordCount = 0;

                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    var hasMatch = false;
                    var candidateScore = int.MaxValue;
                    var candidateWordCount = 0;

                    foreach (var words in tokenSets)
                    {
                        if (!TryCalculateMatchScore(words, candidate.Tokens, out var score)) continue;

                        hasMatch = true;

                        if (score < candidateScore)
                        {
                            candidateScore = score;
                            candidateWordCount = words.Count;

                            if (score == 0) break;
                        }
                    }

                    if (!hasMatch) continue;

                    if (candidateScore < bestScore)
                    {
                        bestScore = candidateScore;
                        bestPath = candidate.Path;
                        bestCandidateIndex = i;
                        bestWordCount = candidateWordCount;

                        if (candidateScore == 0) break;
                    }
                }

                // Require at least one word to score better than the maximum allowed distance to avoid
                // weak matches that only satisfy the fallback threshold.
                if (bestPath is not null
                    && bestCandidateIndex >= 0
                    && bestWordCount > 0
                    && bestScore < bestWordCount * AutomaticConfigMaxWordDistance)
                {
                    results.Add((mod.ModId, bestPath));
                    candidates.RemoveAt(bestCandidateIndex);

                    if (candidates.Count == 0) break;
                }
            }

            return results;
        }

    private static bool TryCalculateMatchScore(
            IReadOnlyList<string> words,
            IReadOnlyList<string> candidateTokens,
            out int score)
        {
            score = 0;
            if (words.Count == 0 || candidateTokens.Count == 0) return false;

            foreach (var word in words)
            {
                var bestDistance = int.MaxValue;
                foreach (var token in candidateTokens)
                {
                    var distance = CalculateBestDistance(token, word, AutomaticConfigMaxWordDistance);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        if (bestDistance == 0) break;
                    }
                }

                if (bestDistance > AutomaticConfigMaxWordDistance)
                {
                    score = int.MaxValue;
                    return false;
                }

                score += bestDistance;
            }

            return true;
        }

    private static int CalculateBestDistance(string token, string word, int maxDistance)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(word)) return int.MaxValue;

            var tokenSpan = token.AsSpan();
            var wordSpan = word.AsSpan();

            return CalculateLevenshteinDistance(wordSpan, tokenSpan, maxDistance);
        }

    private static int CalculateLevenshteinDistance(ReadOnlySpan<char> source, ReadOnlySpan<char> target,
            int maxDistance)
        {
            if (Math.Abs(source.Length - target.Length) > maxDistance) return maxDistance + 1;

            var targetLength = target.Length;
            Span<int> previous = stackalloc int[targetLength + 1];
            Span<int> current = stackalloc int[targetLength + 1];

            for (var j = 0; j <= targetLength; j++) previous[j] = j;

            for (var i = 1; i <= source.Length; i++)
            {
                current[0] = i;
                var minInRow = current[0];
                var sourceChar = source[i - 1];

                for (var j = 1; j <= targetLength; j++)
                {
                    var cost = sourceChar == target[j - 1] ? 0 : 1;
                    var deletion = previous[j] + 1;
                    var insertion = current[j - 1] + 1;
                    var substitution = previous[j - 1] + cost;
                    var value = Math.Min(Math.Min(deletion, insertion), substitution);
                    current[j] = value;

                    if (value < minInRow) minInRow = value;
                }

                if (minInRow > maxDistance) return maxDistance + 1;

                var temp = previous;
                previous = current;
                current = temp;
            }

            return previous[targetLength];
        }

    private static List<IReadOnlyList<string>> BuildModSearchTokenSets(string? modId, string? displayName)
        {
            var tokenSets = new List<IReadOnlyList<string>>();

            AddTokenVariations(displayName);
            AddTokenVariations(modId);

            return tokenSets;

            void AddTokenVariations(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                var tokens = BuildSearchTokens(value, false);
                if (tokens.Count == 0) return;

                AddTokenSet(tokens);

                if (tokens.Count > 1)
                {
                    var combined = string.Concat(tokens);
                    if (!string.IsNullOrWhiteSpace(combined)) AddTokenSet(new List<string> { combined });
                }
            }

            void AddTokenSet(List<string> tokens)
            {
                if (tokens.Count == 0) return;

                foreach (var existing in tokenSets)
                    if (AreTokenListsEqual(existing, tokens))
                        return;

                tokenSets.Add(tokens);
            }
        }

    private static bool AreTokenListsEqual(IReadOnlyList<string> first, IReadOnlyList<string> second)
        {
            if (first.Count != second.Count) return false;

            for (var i = 0; i < first.Count; i++)
                if (!string.Equals(first[i], second[i], StringComparison.Ordinal))
                    return false;

            return true;
        }

    private static List<string> BuildSearchTokens(string value, bool includeCombinedToken = true)
        {
            var tokens = ExtractWords(value);
            if (tokens.Count == 0 && !string.IsNullOrWhiteSpace(value)) tokens.Add(value.ToLowerInvariant());

            if (includeCombinedToken && tokens.Count > 1)
            {
                var combined = string.Concat(tokens);
                if (!string.IsNullOrEmpty(combined) && !tokens.Contains(combined)) tokens.Add(combined);
            }

            return tokens;
        }

    private static List<string> ExtractWords(string value)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return results;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var builder = new StringBuilder();
            var hasPrevious = false;
            var previousChar = '\0';

            void FlushBuilder()
            {
                if (builder.Length == 0) return;

                var word = builder.ToString();
                if (seen.Add(word)) results.Add(word);

                builder.Clear();
            }

            for (var i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (char.IsLetterOrDigit(current))
                {
                    if (builder.Length > 0
                        && char.IsUpper(current)
                        && hasPrevious
                        && char.IsLetter(previousChar)
                        && !char.IsUpper(previousChar))
                        FlushBuilder();

                    builder.Append(char.ToLowerInvariant(current));
                    hasPrevious = true;
                    previousChar = current;
                }
                else
                {
                    FlushBuilder();
                    hasPrevious = false;
                    previousChar = '\0';
                }
            }

            FlushBuilder();

            return results;
        }

}
