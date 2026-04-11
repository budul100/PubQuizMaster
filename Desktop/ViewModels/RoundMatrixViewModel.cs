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

        private readonly QuizNightService nightService;
        private readonly Round round;

        private string? exportError;

        [ObservableProperty] private bool isEditing;

        private bool isFinalRound;

        #endregion Private Fields

        #region Public Constructors

        public RoundMatrixViewModel(QuizNightService nightService, Round round, int roundNumber)
        {
            this.round = round;
            this.nightService = nightService;

            RoundName = round.Name;
            RoundNumber = roundNumber;

            QuestionHeaders = Enumerable
                .Range(1, round.QuestionCount)
                .Select(i => $"Q{i}").ToArray();

            Rebuild();
        }

        #endregion Public Constructors

        #region Public Properties

        public ObservableCollection<int> ColSums { get; } = [];

        public string? ExportError
        {
            get => exportError;
            private set => SetProperty(ref exportError, value);
        }

        public bool IsFinalRound
        {
            get => isFinalRound;
            set => SetProperty(ref isFinalRound, value);
        }

        public Action? OnDeleted { get; set; }

        public Action? OnSaved { get; set; }

        public Func<Task<string?>>? PickTemplateFileAsync { get; set; }

        public int QuestionCount => round.QuestionCount;

        public IReadOnlyList<string> QuestionHeaders { get; }

        public string RoundName { get; }

        public int RoundNumber { get; }

        public ObservableCollection<TeamAnswerRowViewModel> Rows { get; } = [];

        public int TotalCorrectCount => ColSums.Sum();

        #endregion Public Properties

        #region Public Methods

        public void Rebuild()
        {
            Rows.Clear();
            ColSums.Clear();

            var teams = nightService.QuizNight.MasterTeamList
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var team in teams)
            {
                var cells = Enumerable.Range(0, QuestionCount)
                    .Select(qi => new AnswerCellViewModel(round.GetAnswer(team.Id, qi)))
                    .ToList();

                Rows.Add(new TeamAnswerRowViewModel(
                    teamId: team.Id,
                    teamName: team.Name,
                    answers: cells));
            }

            // --- Overall ranks from leaderboard ---
            var leaderboard = nightService.GetLeaderboards();

            AssignRanks(
                Rows.OrderByDescending(r => leaderboard.FirstOrDefault(e => e.Team.Id == r.TeamId)?.TotalScore ?? 0).ToList(),
                keySelector: r => (int)(leaderboard.FirstOrDefault(e => e.Team.Id == r.TeamId)?.TotalScore ?? 0),
                rankSetter: (r, rank) => r.OverallRank = rank);

            // --- Round ranks ---
            AssignRanks(
                Rows.OrderByDescending(r => r.Total).ToList(),
                keySelector: r => r.Total,
                rankSetter: (r, rank) => r.RoundRank = rank);

            // --- Sort rows by round rank for display ---
            var sorted = Rows.OrderBy(r => r.RoundRank).ThenBy(r => r.TeamName).ToList();
            Rows.Clear();
            foreach (var row in sorted)
                Rows.Add(row);

            // --- Column sums ---
            for (var i = 0; i < QuestionCount; i++)
                ColSums.Add(Rows.Count(r => r.Answers[i].IsCorrect == true));

            OnPropertyChanged(nameof(TotalCorrectCount));

            foreach (var row in Rows)
                foreach (var cell in row.Answers)
                    cell.IsEditing = IsEditing;
        }

        #endregion Public Methods

        #region Private Methods

        private static void AssignRanks<T>(
            List<T> ordered,
            Func<T, decimal> keySelector,
            Action<T, int> rankSetter)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                int tieStart = GetTieStart(ordered, i, keySelector);
                rankSetter(ordered[i], tieStart + 1);
            }
        }

        private static int GetTieStart<T>(List<T> ordered, int i, Func<T, decimal> keySelector)
        {
            decimal val = keySelector(ordered[i]);
            int start = i;
            while (start > 0 && keySelector(ordered[start - 1]) == val)
                start--;
            return start;
        }

        [RelayCommand]
        private void Cancel()
        {
            Rebuild();
            IsEditing = false;
        }

        [RelayCommand]
        private async Task DeleteRound()
        {
            nightService.DeleteRound(round.Id);
            await nightService.SaveAsync();
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
            try { templatePath = await PickTemplateFileAsync(); }
            catch (Exception ex) { ExportError = $"File picker error: {ex.Message}"; return; }

            if (string.IsNullOrEmpty(templatePath))
                return;

            try
            {
                var outputPath = await ExportService.ExportAsync(
                    quizSvc: nightService,
                    templatePath: templatePath,
                    isFinalRound: IsFinalRound);

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
            {
                foreach (var cell in row.Answers)
                {
                    cell.IsEditing = value;
                }
            }
        }

        [RelayCommand]
        private void Save()
        {
            foreach (var row in Rows)
                for (var i = 0; i < QuestionCount; i++)
                    nightService.SetAnswer(
                        roundId: round.Id,
                        teamId: row.TeamId,
                        questionIndex: i,
                        isCorrect: row.Answers[i].IsCorrect);

            Rebuild();
            IsEditing = false;
            OnSaved?.Invoke();
        }

        #endregion Private Methods
    }
}