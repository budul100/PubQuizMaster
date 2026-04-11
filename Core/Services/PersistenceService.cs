using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Services
{
    public partial class PersistenceService
    {
        #region Private Fields

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new AnswerBaseJsonConverter() }
        };

        private readonly string storageDirectory;

        #endregion Private Fields

        #region Public Constructors

        public PersistenceService(string? storageDirectory = default)
        {
            var appName = Assembly.GetEntryAssembly()?.GetName().Name
                ?? "PubQuizMaster";

            this.storageDirectory = storageDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    appName);

            Directory.CreateDirectory(this.storageDirectory);
        }

        #endregion Public Constructors

        #region Public Methods

        public static async Task<QuizNight> LoadAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath);

            return JsonSerializer.Deserialize<QuizNight>(json, jsonOptions)
                ?? throw new InvalidDataException($"Failed to deserialize: {filePath}");
        }

        public IEnumerable<FileInfo> ListSavedNights()
        {
            var directoryInfo = new DirectoryInfo(storageDirectory);

            var result = directoryInfo
                .GetFiles("*.json")
                .Where(f => RegexFiles().IsMatch(f.Name)).ToArray();

            return result;
        }

        public async Task SaveAsync(QuizNight quizNight)
        {
            var path = GetFilePath(quizNight);

            var json = JsonSerializer.Serialize(
                value: quizNight,
                options: jsonOptions);

            await File.WriteAllTextAsync(
                path: path,
                contents: json);
        }

        #endregion Public Methods

        #region Private Methods

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}_.+\.json$")]
        private static partial Regex RegexFiles();

        private string GetFilePath(QuizNight quizNight)
        {
            var datePart = quizNight.Date.ToString("yyyy-MM-dd");
            var namePart = quizNight.Name.Length > 20
                ? quizNight.Name[..20]
                : quizNight.Name;

            var filename = $"{datePart}_{namePart}.json"
                .Replace(" ", string.Empty);

            return Path.Combine(
                storageDirectory,
                filename);
        }

        #endregion Private Methods
    }
}