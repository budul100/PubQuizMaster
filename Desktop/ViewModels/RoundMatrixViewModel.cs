using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class RoundMatrixViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly Round _round;
        private readonly QuizNightService _svc;

        [ObservableProperty] private bool _isEditing;

        #endregion Private Fields

        #region Public Constructors

        public RoundMatrixViewModel(Round round, int roundNumber, QuizNightService svc)
        {
            _round = round;
            _svc = svc;
            RoundName = round.Name;
            RoundNumber = roundNumber;

            QuestionHeaders = Enumerable.Range(1, round.QuestionCount)
                                        .Select(i => $"Q{i}")
                                        .ToList();
            Rebuild();
        }

        #endregion Public Constructors

        #region Public Properties

        public ObservableCollection<int> ColSums { get; } = [];

        public Action? ExportAction { get; set; }

        public Action? OnSaved { get; set; }

        public int QuestionCount => _round.QuestionCount;

        // Column headers Q1…Qn
        public IReadOnlyList<string> QuestionHeaders { get; }

        public string RoundName { get; }

        public int RoundNumber { get; }

        public ObservableCollection<TeamAnswerRowViewModel> Rows { get; } = new();

        #endregion Public Properties

        #region Public Methods

        public void Rebuild()
        {
            Rows.Clear();
            ColSums.Clear();

            var teams = _svc.QuizNight.MasterTeamList;
            foreach (var team in teams)
            {
                var cells = Enumerable.Range(0, QuestionCount)
                    .Select(qi => new AnswerCellViewModel(
                        _round.GetAnswer(team.Id, qi)))
                    .ToList();
                Rows.Add(new TeamAnswerRowViewModel(team.Id, team.Name, cells));
            }

            // Column sums
            for (int qi = 0; qi < QuestionCount; qi++)
                ColSums.Add(Rows.Count(r => r.Answers[qi].IsCorrect == true));

            // Am Ende von Rebuild()
            foreach (var row in Rows)
                foreach (var cell in row.Answers)
                    cell.IsEditing = IsEditing;
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void Cancel()
        {
            Rebuild();
            IsEditing = false;
        }

        [RelayCommand]
        private void Edit() => IsEditing = true;

        [RelayCommand]
        private void Export() => ExportAction?.Invoke();

        partial void OnIsEditingChanged(bool value)
        {
            foreach (var row in Rows)
                foreach (var cell in row.Answers)
                    cell.IsEditing = value;
        }

        [RelayCommand]      
        private void Save()
        {
            // Persist edited answers back to the service
            foreach (var row in Rows)
                for (int qi = 0; qi < QuestionCount; qi++)
                    _svc.SetAnswer(_round.Id, row.TeamId, qi, row.Answers[qi].IsCorrect);

            Rebuild();
            IsEditing = false;

            OnSaved?.Invoke();
        }

        #endregion Private Methods
    }
}