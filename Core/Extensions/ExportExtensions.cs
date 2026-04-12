using DocumentFormat.OpenXml.Packaging;
using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Extensions
{
    internal static class ExportExtensions
    {
        #region Public Methods

        public static SlidePart? FindSlide(this IEnumerable<SlidePart> slides, string name) => slides
            .FirstOrDefault(sp => sp?.Slide?.CommonSlideData?.Name?.Value == name);

        public static string FormatAverage(this decimal average)
        {
            var rounded = Math.Round(average, 1);
            var formatted = rounded.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));

            return $"{formatted} Punkte / Team";
        }

        public static string FormatScore(this decimal score)
        {
            var rounded = Math.Round(score, 1);
            return rounded == 1m ? "1 Punkt" : $"{rounded:0.#} Punkte";
        }

        public static IEnumerable<Team> GetAtRank(this IEnumerable<RankedEntry> ranking, int rank) => ranking
            .Where(e => e.Rank == rank)
            .Select(e => e.Team).ToArray();

        public static string GetOutputPath(this string templatePath)
        {
            var dir = Path.GetDirectoryName(templatePath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(templatePath);

            return Path.Combine(
                path1: dir,
                path2: $"{name}_completed.pptx");
        }

        public static string GetPointsText(this Round round, int questionIndex)
        {
            var answers = round.Answers
                .Where(a => a.QuestionIndex == questionIndex)
                .ToList();

            if (!answers.Any()) return string.Empty;

            int total = answers.Count;
            int correct = answers.Count(a => a.Value.GetScore() > 0);

            if (correct == total) return "Alle Teams richtig";
            if (correct == 1) return "1 Team richtig";
            if (correct == 0) return "Kein Team richtig";

            return $"{correct} Teams richtig";
        }

        public static IEnumerable<RankedEntry> GetRanks(this IEnumerable<(Team Team, decimal Score)> scores)
        {
            var ordered = scores
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Team.Name)
                .ToList();

            var rank = 1;
            for (var i = 0; i < ordered.Count; i++)
            {
                if (i > 0 && ordered[i].Score < ordered[i - 1].Score)
                    rank = i + 1;

                yield return new RankedEntry
                {
                    Team = ordered[i].Team,
                    Score = ordered[i].Score,
                    Rank = rank
                };
            }
        }

        #endregion Public Methods
    }
}