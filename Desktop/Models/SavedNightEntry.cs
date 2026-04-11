using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.Models
{
    public partial class SavedNightEntry(FileInfo file)
        : ObservableObject
    {
        #region Public Properties

        public string DisplayName { get; } = Path.GetFileNameWithoutExtension(file.Name);

        public string FilePath { get; } = file.FullName;

        public string LastModified { get; } = file.LastWriteTime.ToString("dd.MM.yyyy HH:mm");

        #endregion Public Properties
    }
}