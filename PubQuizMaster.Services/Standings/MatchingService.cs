using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services
{
    public partial class MatchingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Private Fields

        // Prefix hits rank below complete matches, hits inside the name below prefix hits
        private const double PrefixWeight = 0.9;
        private const double WordWeight = 0.8;

        #endregion Private Fields

        #region Public Methods

        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var clean = PunctuationRegex().Replace(
                input: name.Trim().ToLowerInvariant(),
                replacement: "");

            return WhitespaceRegex().Replace(
                input: clean,
                replacement: " ").Trim();
        }

        public async Task<List<TeamMatch>> FindSimilarTeamsAsync(string candidateName,
            double minThreshold = 0.70, int maxResults = 5, CancellationToken ct = default)
        {
            var normalizedCandidate = Normalize(candidateName);
            if (string.IsNullOrEmpty(normalizedCandidate)) return [];

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var teams = await db.Teams
                .AsNoTracking()
                .Select(t => new { t.Id, t.Name, t.Normalized })
                .ToListAsync(ct);

            var matches = new List<TeamMatch>();

            foreach (var team in teams)
            {
                var target = string.IsNullOrEmpty(team.Normalized)
                    ? Normalize(team.Name)
                    : team.Normalized;

                var similarity = CalculateSimilarity(normalizedCandidate, target);
                if (similarity >= minThreshold)
                {
                    var current = new Team
                    {
                        Id = team.Id,
                        Name = team.Name,
                        Normalized = target
                    };

                    matches.Add(new TeamMatch(
                        Team: current,
                        Similarity: similarity));
                }
            }

            return matches
                .OrderByDescending(m => m.Similarity)
                .Take(maxResults).ToList();
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Best of three views on the candidate, so typing the beginning of a long name already finds it:
        /// 1. Whole name: Levenshtein over the full length, catches typos in complete names.
        /// 2. Prefix: candidate against the beginning of the name, typos included ("quiz mi" finds
        ///    "quiz me baby one more time"). Capped below 1.0 so complete matches rank first.
        /// 3. Word inside the name: candidate starts at a word boundary ("baby one" finds the same team).
        /// </summary>
        private static double CalculateSimilarity(string source, string target)
        {
            if (source == target) return 1.0;
            if (source.Length == 0 || target.Length == 0) return 0.0;

            var whole = LevenshteinSimilarity(source, target);

            if (source.Length >= target.Length) return whole;

            var prefix = LevenshteinSimilarity(source, target[..source.Length]) * PrefixWeight;

            var word = target.Contains($" {source}", StringComparison.Ordinal)
                ? WordWeight
                : 0.0;

            return Math.Max(whole, Math.Max(prefix, word));
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        private static double LevenshteinSimilarity(string source, string target)
        {
            var distance = LevenshteinDistance(source, target);
            var maxLength = Math.Max(source.Length, target.Length);

            return 1.0 - ((double)distance / maxLength);
        }

        [GeneratedRegex(@"[^\w\d\s]")]
        private static partial Regex PunctuationRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        #endregion Private Methods
    }
}