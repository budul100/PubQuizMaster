using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Import;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Import
{
    /// <summary>
    /// Imports historic aggregated results. Idempotent: a result per quiz night and team
    /// is created once and updated on later imports.
    /// Rows without team name import the quiz night only (e.g. with a remark).
    /// A team's creation date is the date of the first quiz night it appears in.
    /// Optional columns G (questions) and H (rounds) describe the night, they may appear on any of its rows.
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
            var updatedQuizIds = new HashSet<Guid>();
            var teamsCreated = 0;
            var resultsCreated = 0;
            var resultsUpdated = 0;
            var resultsUnchanged = 0;

            // Teams of this import, to tell new teams from existing ones with a corrected creation date
            var createdTeamIds = new HashSet<Guid>();
            var backdatedTeamIds = new HashSet<Guid>();

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var teamLookup = await LoadTeamLookupAsync(db, ct);
            var (quizLookup, resultLookup) = await LoadLegacyQuizLookupsAsync(db, ct);
            var liveQuizKeys = await LoadLiveQuizKeysAsync(db, ct);

            // Guards against the same team appearing twice for one night within the file
            var processedEntries = new HashSet<(string QuizKey, string TeamKey)>();

            // First question and round count per night in the file, later rows must agree
            var fileCounts = new Dictionary<string, (int? Questions, int? Rounds)>();

            foreach (var row in worksheet.RowsUsed().Skip(1)) // Skip header row
            {
                var rowNumber = row.RowNumber();

                var dateCell = row.Cell("A");
                var titleCell = row.Cell("B");
                var descCell = row.Cell("C");
                var rankCell = row.Cell("D");
                var teamCell = row.Cell("E");
                var scoreCell = row.Cell("F");
                var questionsCell = row.Cell("G");
                var roundsCell = row.Cell("H");

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

                var questionCount = ParseCount(questionsCell);
                var roundCount = ParseCount(roundsCell);

                if (questionCount == null && !questionsCell.IsEmpty()
                    || roundCount == null && !roundsCell.IsEmpty())
                {
                    warnings.Add($"Row {rowNumber}: Invalid question or round count. Ignored.");
                }

                // 3. Team
                var rawTeamName = teamCell.GetString().Trim();

                if (string.IsNullOrEmpty(rawTeamName))
                {
                    // Quiz night without results, kept in the list with its remark
                    var emptyNight = GetOrCreateQuiz(quizKey, title, description, quizDate);
                    ApplyCounts(emptyNight, quizKey, questionCount, roundCount, rowNumber);

                    if (!rankCell.IsEmpty() || !scoreCell.IsEmpty())
                    {
                        warnings.Add($"Row {rowNumber}: Rank or points without team name. Only the quiz night was imported.");
                    }

                    continue;
                }

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
                // Midnight UTC of the quiz date: the teams page shows the stored date without conversion
                var appearedAt = quizDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

                if (!teamLookup.TryGetValue(teamKey, out var team))
                {
                    team = new Team
                    {
                        Name = rawTeamName,
                        Normalized = teamKey,
                        CreatedAt = appearedAt
                    };

                    db.Teams.Add(team);
                    teamLookup[teamKey] = team;
                    createdTeamIds.Add(team.Id);
                    teamsCreated++;
                }
                else if (team.CreatedAt > appearedAt)
                {
                    // Rows come in any order, and earlier imports stamped the import time
                    team.CreatedAt = appearedAt;

                    if (!createdTeamIds.Contains(team.Id))
                    {
                        backdatedTeamIds.Add(team.Id);
                    }
                }

                var quizNight = GetOrCreateQuiz(quizKey, title, description, quizDate);
                ApplyCounts(quizNight, quizKey, questionCount, roundCount, rowNumber);

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

            return new ImportSummary(
                QuizzesCreated: quizzesCreated,
                QuizzesUpdated: updatedQuizIds.Count,
                TeamsCreated: teamsCreated,
                TeamsBackdated: backdatedTeamIds.Count,
                ResultsCreated: resultsCreated,
                ResultsUpdated: resultsUpdated,
                ResultsUnchanged: resultsUnchanged,
                Warnings: [.. warnings]);

            // The first value per night in the file wins, the file overrides earlier imports
            void ApplyCounts(Quiz quiz, string quizKey, int? questions, int? rounds, int rowNumber)
            {
                if (questions == null && rounds == null) return;

                if (fileCounts.TryGetValue(quizKey, out var first))
                {
                    if (questions != null && first.Questions != null && questions != first.Questions
                        || rounds != null && first.Rounds != null && rounds != first.Rounds)
                    {
                        warnings.Add($"Row {rowNumber}: Question or round count differs from an earlier row " +
                            $"of '{quiz.Title}' on {quiz.Date:dd.MM.yyyy}. The earlier value is kept.");
                    }

                    questions = first.Questions ?? questions;
                    rounds = first.Rounds ?? rounds;
                }

                fileCounts[quizKey] = (questions, rounds);

                var isChanged = questions != null && quiz.ImportedQuestionCount != questions
                    || rounds != null && quiz.ImportedRoundCount != rounds;

                if (!isChanged) return;

                quiz.ImportedQuestionCount = questions ?? quiz.ImportedQuestionCount;
                quiz.ImportedRoundCount = rounds ?? quiz.ImportedRoundCount;

                // Only existing nights count as updated, new ones are counted as created
                if (db.Entry(quiz).State != EntityState.Added)
                {
                    updatedQuizIds.Add(quiz.Id);
                }
            }

            Quiz GetOrCreateQuiz(string quizKey, string title, string? description, DateOnly date)
            {
                if (quizLookup.TryGetValue(quizKey, out var quiz))
                {
                    if (description != null && string.IsNullOrEmpty(quiz.Description))
                    {
                        quiz.Description = description;
                    }

                    return quiz;
                }

                quiz = new Quiz
                {
                    Title = title,
                    Description = description,
                    Date = date,
                    Status = QuizStatus.Completed,
                    IsLegacyImport = true
                };

                db.Quizzes.Add(quiz);
                quizLookup[quizKey] = quiz;
                quizzesCreated++;

                return quiz;
            }
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

        /// <summary>Positive whole number, null if the cell is empty or holds anything else.</summary>
        private static int? ParseCount(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;

            int value;

            if (cell.DataType == XLDataType.Number)
            {
                value = (int)Math.Round(cell.GetDouble());
            }
            else if (!int.TryParse(cell.GetString().Trim(), out value))
            {
                return null;
            }

            return value > 0 ? value : null;
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


