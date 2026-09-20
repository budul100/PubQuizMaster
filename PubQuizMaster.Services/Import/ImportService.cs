using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Import;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Import
{
    /// <summary>
    /// Imports historic aggregated results. Idempotent: a result per quiz night and team
    /// is created once and updated on later imports.
    /// </summary>
    public class ImportService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<ImportSummary> ImportFromExcelAsync(
            Stream excelStream,
            string? worksheetName = null,
            CancellationToken ct = default)
        {
            // ClosedXML reads synchronously and needs a seekable stream, browser upload streams are neither
            await using var buffer = new MemoryStream();
            await excelStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            using var workbook = new XLWorkbook(buffer);
            var worksheet = string.IsNullOrWhiteSpace(worksheetName)
                ? workbook.Worksheets.First()
                : workbook.Worksheet(worksheetName);

            var warnings = new List<string>();
            var quizzesCreated = 0;
            var teamsCreated = 0;
            var resultsCreated = 0;
            var resultsUpdated = 0;
            var resultsUnchanged = 0;

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var teamLookup = await LoadTeamLookupAsync(db, ct);
            var (quizLookup, resultLookup) = await LoadLegacyQuizLookupsAsync(db, ct);
            var liveQuizKeys = await LoadLiveQuizKeysAsync(db, ct);

            // Guards against the same team appearing twice for one night within the file
            var processedEntries = new HashSet<(string QuizKey, string TeamKey)>();

            foreach (var row in worksheet.RowsUsed().Skip(1)) // Skip header row
            {
                var rowNumber = row.RowNumber();

                var dateCell = row.Cell("A");
                var titleCell = row.Cell("B");
                var descCell = row.Cell("C");
                var rankCell = row.Cell("D");
                var teamCell = row.Cell("E");
                var scoreCell = row.Cell("F");

                if (dateCell.IsEmpty() && teamCell.IsEmpty()) continue;

                // 1. Date
                if (!TryParseDate(dateCell, out var quizDate))
                {
                    warnings.Add($"Row {rowNumber}: Invalid date format '{dateCell.GetString()}'. Skipped.");
                    continue;
                }

                // 2. Title and description
                var title = titleCell.GetString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Pub Quiz ({quizDate:dd.MM.yyyy})";
                }

                var description = descCell.GetString().Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = null;
                }

                var quizKey = CreateQuizKey(quizDate, title);
                if (liveQuizKeys.Contains(quizKey))
                {
                    warnings.Add($"Row {rowNumber}: '{title}' on {quizDate:dd.MM.yyyy} exists as a live quiz night. Skipped.");
                    continue;
                }

                // 3. Team
                var rawTeamName = teamCell.GetString().Trim();
                var teamKey = MatchingService.Normalize(rawTeamName);
                if (string.IsNullOrEmpty(teamKey))
                {
                    warnings.Add($"Row {rowNumber}: Missing or invalid team name '{rawTeamName}'. Skipped.");
                    continue;
                }

                if (!processedEntries.Add((quizKey, teamKey)))
                {
                    warnings.Add($"Row {rowNumber}: Team '{rawTeamName}' appears more than once for '{title}' on {quizDate:dd.MM.yyyy}. Skipped.");
                    continue;
                }

                // 4. Score and rank
                if (!TryParseScore(scoreCell, out var totalScore))
                {
                    warnings.Add($"Row {rowNumber}: Invalid score for team '{rawTeamName}'. Defaulted to 0.");
                }

                var rank = ParseRank(rankCell);

                // 5. Find or create team and quiz night
                if (!teamLookup.TryGetValue(teamKey, out var team))
                {
                    team = new Team
                    {
                        Name = rawTeamName,
                        Normalized = teamKey,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Teams.Add(team);
                    teamLookup[teamKey] = team;
                    teamsCreated++;
                }

                if (!quizLookup.TryGetValue(quizKey, out var quizNight))
                {
                    quizNight = new Quiz
                    {
                        Title = title,
                        Description = description,
                        Date = quizDate,
                        IsCompleted = true,
                        IsLegacyImport = true
                    };

                    db.Quizzes.Add(quizNight);
                    quizLookup[quizKey] = quizNight;
                    quizzesCreated++;
                }
                else if (description != null && string.IsNullOrEmpty(quizNight.Description))
                {
                    quizNight.Description = description;
                }

                // 6. Create or update result
                if (resultLookup.TryGetValue((quizNight.Id, team.Id), out var existing))
                {
                    if (existing.TotalScore == totalScore && existing.Rank == rank)
                    {
                        resultsUnchanged++;
                        continue;
                    }

                    existing.TotalScore = totalScore;
                    existing.Rank = rank;
                    resultsUpdated++;
                    continue;
                }

                var result = new Result
                {
                    QuizId = quizNight.Id,
                    TeamId = team.Id,
                    TotalScore = totalScore,
                    Rank = rank
                };

                db.Scores.Add(result);
                resultLookup[(quizNight.Id, team.Id)] = result;
                resultsCreated++;
            }

            await db.SaveChangesAsync(ct);

            return new ImportSummary(quizzesCreated, teamsCreated, resultsCreated, resultsUpdated,
                resultsUnchanged, [.. warnings]);
        }

        #endregion Public Methods

        #region Private Methods

        private static string CreateQuizKey(DateOnly date, string title)
        {
            return $"{date:yyyy-MM-dd}_{title.Trim().ToLowerInvariant()}";
        }

        private static async Task<(Dictionary<string, Quiz> Quizzes, Dictionary<(Guid QuizId, Guid TeamId), Result> Results)>
            LoadLegacyQuizLookupsAsync(AppDbContext db, CancellationToken ct)
        {
            var legacyQuizzes = await db.Quizzes
                .Where(q => q.IsLegacyImport)
                .Include(q => q.Results)
                .ToArrayAsync(ct);

            var quizzes = new Dictionary<string, Quiz>();
            var results = new Dictionary<(Guid QuizId, Guid TeamId), Result>();

            foreach (var quiz in legacyQuizzes)
            {
                // Titles can be edited later, so two legacy nights may share a key. The first one wins.
                quizzes.TryAdd(CreateQuizKey(quiz.Date, quiz.Title), quiz);

                foreach (var result in quiz.Results)
                {
                    results.TryAdd((result.QuizId, result.TeamId), result);
                }
            }

            return (quizzes, results);
        }

        private static async Task<HashSet<string>> LoadLiveQuizKeysAsync(AppDbContext db, CancellationToken ct)
        {
            var liveQuizzes = await db.Quizzes
                .AsNoTracking()
                .Where(q => !q.IsLegacyImport)
                .Select(q => new { q.Date, q.Title })
                .ToArrayAsync(ct);

            return liveQuizzes
                .Select(q => CreateQuizKey(q.Date, q.Title))
                .ToHashSet();
        }

        private static async Task<Dictionary<string, Team>> LoadTeamLookupAsync(AppDbContext db, CancellationToken ct)
        {
            var teams = await db.Teams.ToArrayAsync(ct);
            var lookup = new Dictionary<string, Team>();

            foreach (var team in teams)
            {
                var key = string.IsNullOrEmpty(team.Normalized)
                    ? MatchingService.Normalize(team.Name)
                    : team.Normalized;

                // Legacy rows without NormalizedName can collide with regular ones. The first one wins.
                if (!string.IsNullOrEmpty(key))
                {
                    lookup.TryAdd(key, team);
                }
            }

            return lookup;
        }

        private static int? ParseRank(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;

            if (cell.DataType == XLDataType.Number)
            {
                return (int)Math.Round(cell.GetDouble());
            }

            return int.TryParse(cell.GetString().Trim(), out var rank) ? rank : null;
        }

        private static bool TryParseDate(IXLCell cell, out DateOnly date)
        {
            if (cell.DataType == XLDataType.DateTime)
            {
                date = DateOnly.FromDateTime(cell.GetDateTime());
                return true;
            }

            return DateOnly.TryParse(cell.GetString().Trim(), out date);
        }

        private static bool TryParseScore(IXLCell cell, out decimal score)
        {
            score = 0m;
            if (cell.IsEmpty()) return true;

            if (cell.DataType == XLDataType.Number)
            {
                score = Convert.ToDecimal(cell.GetDouble());
                return true;
            }

            if (decimal.TryParse(cell.GetString().Trim().Replace(',', '.'),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                score = parsed;
                return true;
            }

            return false;
        }

        #endregion Private Methods
    }
}
