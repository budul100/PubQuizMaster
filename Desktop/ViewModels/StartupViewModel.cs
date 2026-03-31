using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.Models;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class StartupViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly PersistenceService _persistence;

        [ObservableProperty] private string _errorMessage = string.Empty;

        [ObservableProperty] private DateTimeOffset _newNightDate = DateTimeOffset.Now;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateNewCommand))]
        private string _newNightName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
        private SavedNightEntry? _selectedNight;

        #endregion Private Fields

        #region Public Constructors

        public StartupViewModel(PersistenceService persistence)
        {
            _persistence = persistence;
            LoadSavedNights();
        }

        #endregion Public Constructors

        #region Public Properties

        public Action? CloseRequested { get; set; }

        public QuizNight? Result { get; private set; }

        public ObservableCollection<SavedNightEntry> SavedNights { get; } = new();

        #endregion Public Properties

        #region Private Methods

        private bool CanCreateNew() =>
            !string.IsNullOrWhiteSpace(NewNightName);

        private bool CanLoad() => SelectedNight != null;

        [RelayCommand(CanExecute = nameof(CanCreateNew))]
        private void CreateNew()
        {
            Result = new QuizNight
            {
                Name = NewNightName.Trim(),
                Date = NewNightDate.DateTime
            };
            CloseRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private async Task Load()
        {
            if (SelectedNight == null) return;

            try
            {
                Result = await _persistence.LoadAsync(SelectedNight.FilePath);
                CloseRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load file: {ex.Message}";
            }
        }

        private void LoadSavedNights()
        {
            SavedNights.Clear();
            foreach (var file in _persistence.ListSavedNights())
                SavedNights.Add(new SavedNightEntry(file));
        }

        #endregion Private Methods
    }
}