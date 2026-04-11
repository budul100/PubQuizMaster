using System.Text.Json;
using PubQuizMaster.Core.Models;

namespace PubQuizMaster.Core.Services
{
    public class SettingsService
    {
        #region Private Fields

        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
        private readonly string _path;

        #endregion Private Fields

        #region Public Constructors

        public SettingsService()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PubQuizMaster");

            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
        }

        #endregion Public Constructors

        #region Public Properties

        public Settings Settings { get; private set; } = new();

        #endregion Public Properties

        #region Public Methods

        public void ClearUrl()
        {
            Settings.ClientUrl = null;
            Save();
        }

        public void Load()
        {
            if (!File.Exists(_path)) return;
            try
            {
                Settings = JsonSerializer.Deserialize<Settings>(
                    File.ReadAllText(_path), _json) ?? new();
            }
            catch { Settings = new(); }
        }

        public void Save()
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, _json));
        }

        #endregion Public Methods
    }
}