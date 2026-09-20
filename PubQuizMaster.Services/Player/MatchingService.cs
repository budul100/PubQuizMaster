using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services
{
    public partial class MatchingService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var clean = PunctuationRegex().Replace(name.Trim().ToLowerInvariant(), "");
            return WhitespaceRegex().Replace(clean, " ").Trim();
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
                    matches.Add(new TeamMatch(
                        new Team { Id = team.Id, Name = team.Name, Normalized = target },
                        similarity));
                }
            }

            return matches
                .OrderByDescending(m => m.Similarity)
                .Take(maxResults).ToList();
        }

        #endregion Public Methods

        #region Private Methods

        private static double CalculateSimilarity(string source, string target)
        {
            if (source == target) return 1.0;
            if (source.Length == 0 || target.Length == 0) return 0.0;

            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            return 1.0 - ((double)distance / maxLength);
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

        [GeneratedRegex(@"[^\w\d\s]")]
        private static partial Regex PunctuationRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        #endregion Private Methods
    }
}