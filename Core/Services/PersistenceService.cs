using PubQuizMaster.Core.Models.Event;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubQuizMaster.Core.Services
{
    // ─────────────────────────────────────────────
    // PERSISTENCE SERVICE
    // Handles JSON save/load of the QuizNight model.
    // Single file per quiz night, stored in AppData or next to the executable.
    // ─────────────────────────────────────────────

    /// <summary>
    /// Persists and restores QuizNight objects as JSON files.
    /// File naming convention: "quiznight_{date}_{id_short}.json"
    /// </summary>
    public class PersistenceService
    {
        private readonly string _storageDirectory;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new AnswerBaseJsonConverter() }
        };

        public PersistenceService(string? storageDirectory = null)
        {
            _storageDirectory = storageDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PubQuizMaster");

            Directory.CreateDirectory(_storageDirectory);
        }

        /// <summary>Saves the quiz night to disk. Overwrites if file exists.</summary>
        public async Task SaveAsync(QuizNight quizNight)
        {
            var path = GetFilePath(quizNight);
            var json = JsonSerializer.Serialize(quizNight, _jsonOptions);
            await File.WriteAllTextAsync(path, json);
        }

        /// <summary>Loads a quiz night by its file path.</summary>
        public async Task<QuizNight> LoadAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<QuizNight>(json, _jsonOptions)
                   ?? throw new InvalidDataException($"Failed to deserialize: {filePath}");
        }

        /// <summary>Returns all saved quiz night files, newest first.</summary>
        public IEnumerable<FileInfo> ListSavedNights()
        {
            return new DirectoryInfo(_storageDirectory)
                .GetFiles("quiznight_*.json")
                .OrderByDescending(f => f.LastWriteTime);
        }

        private string GetFilePath(QuizNight quizNight)
        {
            var datePart = quizNight.Date.ToString("yyyy-MM-dd");
            var idPart = quizNight.Id.ToString()[..8];
            return Path.Combine(_storageDirectory, $"quiznight_{datePart}_{idPart}.json");
        }
    }
}