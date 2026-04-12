using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class LeftPanelViewModel(DataService dataService)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool canAddRound;

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
            foreach (var round in Rounds)
            {
                round.IsSelected = false;
            }
        }

        public void Refresh(bool roundIsActive, bool setupIsActive = false)
        {
            CanAddRound = !roundIsActive
                && !setupIsActive
                && dataService.QuizNight.Rounds.All(r => r.IsFinalized);

            Rounds.Clear();

            foreach (var round in dataService.QuizNight.Rounds)
            {
                Rounds.Add(new RoundEntryViewModel(
                    round: round,
                    quizNight: dataService.QuizNight,
                    onSelect: entry => OnRoundSelected?.Invoke(entry)));
            }

            TotalBoard.Clear();

            var leaderBoards = dataService
                .GetLeaderboards().ToArray();

            foreach (var leaderBoard in leaderBoards)
            {
                TotalBoard.Add(new LeaderboardEntryViewModel(
                    rank: leaderBoard.Rank,
                    score: leaderBoard.TotalScore,
                    teamName: leaderBoard.Team.Name));
            }
        }

        public void SelectEntry(Guid roundId)
        {
            foreach (var round in Rounds)
            {
                round.IsSelected = round.RoundId == roundId;
            }
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void NewRound() => OnNewRound?.Invoke();

        #endregion Private Methods
    }
}