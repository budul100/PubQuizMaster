using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly QuizNightService _svc;      // nur noch eins
        private string? _exportError;
        [ObservableProperty] private bool _isEditing;
        private bool _isFinalRound;

        #endregion Private Fields

        #region Public Constructors

        public RoundMatrixViewModel(QuizNightService svc, Round round, int roundNumber)
        {
            _round = round;
            _svc = svc;

            RoundName = round.Name;
            RoundNumber = roundNumber;

            QuestionHeaders = Enumerable
                .Range(1, round.QuestionCount)
                .Select(i => $"Q{i}")
                .ToList();

            Rebuild();
        }

        #endregion Public Constructors

        #region Public Properties

        public ObservableCollection<int> ColSums { get; } = [];

        public string? ExportError
        {
            get => _exportError;
            private set => SetProperty(ref _exportError, value);
        }

        public bool IsFinalRound
        {
            get => _isFinalRound;
            set => SetProperty(ref _isFinalRound, value);
        }

        public Action? OnDeleted { get; set; }

        public Action? OnSaved { get; set; }

        public Func<Task<string?>>? PickTemplateFileAsync { get; set; }

        public int QuestionCount => _round.QuestionCount;

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

            var teams = _svc.QuizNight.MasterTeamList
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

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
        private async Task DeleteRound()
        {
            _svc.DeleteRound(_round.Id);
            await _svc.SaveAsync();
            OnDeleted?.Invoke();
        }

        [RelayCommand]
        private void Edit() => IsEditing = true;

        [RelayCommand]
        private async Task Export()
        {
            ExportError = null;

            if (PickTemplateFileAsync == null)
            {
                ExportError = "File picker not available.";
                return;
            }

            string? templatePath;
            try
            {
                templatePath = await PickTemplateFileAsync();
            }
            catch (Exception ex)
            {
                ExportError = $"File picker error: {ex.Message}";
                return;
            }

            if (string.IsNullOrEmpty(templatePath))
                return;

            try
            {
                var outputPath = await ExportService.ExportAsync(_svc, templatePath, IsFinalRound);
                System.Diagnostics.Debug.WriteLine($"[Export] Completed: {outputPath}");
            }
            catch (Exception ex)
            {
                ExportError = $"Export failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[Export] ERROR: {ex}");
            }
        }

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