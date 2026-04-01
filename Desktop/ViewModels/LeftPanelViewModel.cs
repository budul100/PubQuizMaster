using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class LeftPanelViewModel(QuizNightService svc)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool _canAddRound;

        #endregion Private Fields

        #region Public Properties

        public Action? OnNewRound { get; set; }

        public Action<RoundEntryViewModel>? OnRoundSelected { get; set; }

        public ObservableCollection<RoundEntryViewModel> Rounds { get; } = [];

        public ObservableCollection<LeaderboardEntryViewModel> TotalBoard { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public void ClearSelection()
        {
            foreach (var r in Rounds) r.IsSelected = false;
        }

        public void Refresh(bool roundIsActive, bool setupIsActive = false)
        {
            CanAddRound = !roundIsActive && !setupIsActive
                && svc.QuizNight.Rounds.All(r => r.IsFinalized);

            Rounds.Clear();
            foreach (var r in svc.QuizNight.Rounds)
                Rounds.Add(new RoundEntryViewModel(
                    r, svc.QuizNight,
                    entry => OnRoundSelected?.Invoke(entry)));

            TotalBoard.Clear();
            var lb = svc.GetLeaderboard();
            int rank = 1;
            foreach (var e in lb)
                TotalBoard.Add(new LeaderboardEntryViewModel
                { Rank = rank++, TeamName = e.Team.Name, Score = e.TotalScore });
        }

        public void SelectEntry(Guid roundId)
        {
            foreach (var r in Rounds)
                r.IsSelected = r.RoundId == roundId;
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void NewRound() => OnNewRound?.Invoke();

        #endregion Private Methods
    }
}