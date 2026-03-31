// ===== SavedNightEntry.cs =====
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.Models
{
    public partial class SavedNightEntry
        : ObservableObject
    {
        #region Public Constructors

        public SavedNightEntry(FileInfo file)
        {
            FilePath = file.FullName;
            DisplayName = Path.GetFileNameWithoutExtension(file.Name);
            LastModified = file.LastWriteTime.ToString("dd.MM.yyyy HH:mm");
        }

        #endregion Public Constructors

        #region Public Properties

        public string DisplayName { get; }

        public string FilePath { get; }

        public string LastModified { get; }

        #endregion Public Properties
    }
}