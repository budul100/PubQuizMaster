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
    public partial class StartupViewModel : ViewModelBase
    {
        #region Private Fields

        private readonly PersistenceService _persistence;
        private string _clientUrl = string.Empty;

        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private DateTime? _newNightDate = DateTime.Today;

        [NotifyCanExecuteChangedFor(nameof(CreateNewCommand))]
        [ObservableProperty] private string _newNightName = string.Empty;

        [ObservableProperty] private ConnectionMode _selectedMode = ConnectionMode.Local;

        [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
        [ObservableProperty] private SavedNightEntry? _selectedNight;

        #endregion Private Fields

        #region Public Constructors

        public StartupViewModel(PersistenceService persistence)
        {
            _persistence = persistence;
            LoadSavedNights();

            // Start in Local mode — set local IP immediately
            _clientUrl = $"http://{Web.KestrelHost.GetLocalIpAddress()}:{Web.KestrelHost.Port}";
        }

        #endregion Public Constructors

        #region Public Properties

        public string ClientUrl
        {
            get => _clientUrl;
            set
            {
                if (SetProperty(ref _clientUrl, value))
                {
                    if (SelectedMode == ConnectionMode.Tunnel)
                    {
                        App.SettingsService.Settings.ClientUrl = value;
                        App.SettingsService.Save();
                    }
                    OnPropertyChanged(nameof(IsConnected));
                }
            }
        }

        public Action? CloseRequested { get; set; }
        public bool IsConnected => !string.IsNullOrWhiteSpace(ClientUrl);

        public bool IsLocalMode => SelectedMode == ConnectionMode.Local;

        public bool IsTunnelMode
        {
            get => SelectedMode == ConnectionMode.Tunnel;
            set
            {
                SelectedMode = value ? ConnectionMode.Tunnel : ConnectionMode.Local;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocalMode));
            }
        }

        public QuizNight? Result { get; private set; }

        public ObservableCollection<SavedNightEntry> SavedNights { get; } = new();

        #endregion Public Properties

        #region Private Methods

        private bool CanCreateNew() => !string.IsNullOrWhiteSpace(NewNightName);

        private bool CanLoad() => SelectedNight != null;

        [RelayCommand(CanExecute = nameof(CanCreateNew))]
        private void CreateNew()
        {
            Result = new QuizNight
            {
                Name = NewNightName.Trim(),
                Date = NewNightDate ?? DateTime.Today,
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

        partial void OnSelectedModeChanged(ConnectionMode value)
        {
            if (value == ConnectionMode.Local)
            {
                ClientUrl = $"http://{Web.KestrelHost.GetLocalIpAddress()}:{Web.KestrelHost.Port}";

                App.SettingsService.Settings.ClientUrl = null;
                App.SettingsService.Save();
            }
            else
            {
                var saved = App.SettingsService.Settings.ClientUrl;
                ClientUrl = !string.IsNullOrWhiteSpace(saved) ? saved : string.Empty;
            }
        }

        #endregion Private Methods
    }
}