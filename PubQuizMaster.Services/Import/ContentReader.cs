using System.Text.Json;
using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Records.Import;
using PubQuizMaster.Services.Event;

namespace PubQuizMaster.Services.Import
{
    /// <summary>
    /// Reads the JSON quiz export of the question editor and brings it into the stored form:
    /// rounds and questions sorted by position, strings trimmed, structure validated.
    /// </summary>
    public static class ContentReader
    {
        #region Public Fields

        // The export of a full night is about 25 KB, the limit only guards against wrong files
        public const long MaxFileSize = 1024 * 1024;

        #endregion Public Fields

        #region Private Fields

        private static readonly JsonSerializerOptions readOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        #endregion Private Fields

        #region Public Methods

        public static async Task<QuizImport> ReadAsync(Stream stream, CancellationToken ct = default)
        {
            QuizImport? import;

            try
            {
                // Deserializes asynchronously, browser upload streams do not support synchronous reads
                import = await JsonSerializer.DeserializeAsync<QuizImport>(stream, readOptions, ct);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"The file is not a valid quiz export: {ex.Message}", ex);
            }

            if (import == null)
            {
                throw new InvalidOperationException("The file is empty.");
            }

            return Normalize(import);
        }

        #endregion Public Methods

        #region Private Methods

        private static Question Normalize(Question question) => new()
        {
            Position = question.Position,
            Category = question.Category?.Trim() ?? string.Empty,
            Text = question.Text?.Trim() ?? string.Empty,
            Description = question.Description?.Trim() ?? string.Empty,
            Answer = question.Answer?.Trim() ?? string.Empty,
        };

        private static Round Normalize(Round round)
        {
            var questions = (round.Questions ?? [])
                .OrderBy(q => q.Position)
                .Select(Normalize)
                .ToList();

            if (questions.Count is < 1 or > QuizService.MaxQuestionCount)
            {
                throw new InvalidOperationException(
                    $"Round {round.Position} has {questions.Count} questions, " +
                    $"allowed are 1 to {QuizService.MaxQuestionCount}.");
            }

            var duplicate = questions
                .GroupBy(q => q.Position)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    $"Round {round.Position} contains question position {duplicate.Key} more than once.");
            }

            return new Round
            {
                Position = round.Position,
                Questions = questions,
            };
        }

        private static QuizImport Normalize(QuizImport import)
        {
            // Missing JSON properties arrive as null despite the non-nullable declarations
            var rounds = (import.Rounds ?? [])
                .OrderBy(r => r.Position)
                .Select(Normalize)
                .ToList();

            if (rounds.Count == 0)
            {
                throw new InvalidOperationException("The file contains no rounds.");
            }

            var duplicate = rounds
                .GroupBy(r => r.Position)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException($"Round position {duplicate.Key} occurs more than once.");
            }

            return new QuizImport(
                Title: import.Title?.Trim() ?? string.Empty,
                Date: import.Date,
                Rounds: rounds);
        }

        #endregion Private Methods
    }
}
