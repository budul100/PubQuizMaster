using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Desktop.Models;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class RoundEntryViewModel
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool _isSelected;

        #endregion Private Fields

        #region Public Constructors

        public RoundEntryViewModel(Round round, QuizNight quizNight, Action<RoundEntryViewModel> onSelect)
        {
            RoundId = round.Id;
            RoundName = round.Name;
            IsFinalized = round.IsFinalized;
            QuestionCount = round.QuestionCount;

            TeamScores = quizNight.MasterTeamList
                .Select(t => new TeamScoreEntry(t.Name, round.GetTeamScore(t.Id)))
                .Where(x => x.Score.HasValue)
                .OrderByDescending(x => x.Score)
                .ToList();

            QuestionCorrectCounts = Enumerable.Range(0, round.QuestionCount)
                .Select(qi => round.Answers.Count(a =>
                    a.QuestionIndex == qi && a.Value.GetScore() > 0))
                .ToList();

            SelectCommand = new RelayCommand(() => onSelect(this));
        }

        #endregion Public Constructors

        #region Public Properties

        public bool IsFinalized { get; }

        // How many teams answered each question correctly
        public IReadOnlyList<int> QuestionCorrectCounts { get; }

        public int QuestionCount { get; }

        public Guid RoundId { get; }

        public string RoundName { get; }

        public IRelayCommand SelectCommand { get; }

        public List<TeamScoreEntry> TeamScores { get; } = [];

        #endregion Public Properties
    }
}