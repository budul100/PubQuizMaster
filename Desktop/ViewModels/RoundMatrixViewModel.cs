using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

        private readonly DataService dataService;
        private readonly Round round;

        [ObservableProperty] private string? exportError;
        [ObservableProperty] private string? exportSuccess;
        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private bool isExporting;
        [ObservableProperty] private bool isFinalRound;

        #endregion Private Fields

        #region Public Constructors

        public RoundMatrixViewModel(DataService dataService, Round round, int roundNumber)
        {
            this.round = round;
            this.dataService = dataService;

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

            var teams = dataService.QuizNight.MasterTeamList
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

            var ranksRound = Rows
                .OrderByDescending(r => r.Total).ToList();

            AssignRanks(
                ordered: ranksRound,
                keySelector: r => r.Total,
                rankSetter: (r, rank) => r.RoundRank = rank);

            var leaderboard = dataService.GetLeaderboardsUpTo(round.Id);

            var ranksTotal = Rows
                .OrderByDescending(r => leaderboard.FirstOrDefault(e => e.Team.Id == r.TeamId)?.TotalScore ?? 0).ToList();

            AssignRanks(
                ordered: ranksTotal,
                keySelector: r => leaderboard.FirstOrDefault(e => e.Team.Id == r.TeamId)?.TotalScore ?? 0,
                rankSetter: (r, rank) => r.OverallRank = rank);

            var sorted = Rows
                .OrderBy(r => r.RoundRank).ThenBy(r => r.TeamName).ToList();

            Rows.Clear();

            foreach (var row in sorted)
            {
                Rows.Add(row);
            }

            for (var i = 0; i < QuestionCount; i++)
            {
                ColSums.Add(Rows.Count(r => r.Answers[i].IsCorrect == true));
            }

            OnPropertyChanged(nameof(TotalCorrectCount));

            foreach (var row in Rows)
            {
                foreach (var cell in row.Answers)
                {
                    cell.IsEditing = IsEditing;
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        private static void AssignRanks<T>(List<T> ordered, Func<T, decimal> keySelector, Action<T, int> rankSetter)
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
            {
                start--;
            }

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
            dataService.DeleteRound(round.Id);
            await dataService.SaveAsync();
            OnDeleted?.Invoke();
        }

        [RelayCommand]
        private void Edit() => IsEditing = true;

        [RelayCommand]
        private async Task Export()
        {
            ExportError = null;
            ExportSuccess = null;

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

            IsExporting = true;

            try
            {
                var outputPath = await Task.Run(() => ExportService.ExportAsync(
                    dataService: dataService,
                    roundId: round.Id,
                    isFinalRound: IsFinalRound,
                    templatePath: templatePath));

                ExportSuccess = $"✓ Export done: {Path.GetFileName(outputPath)}";
            }
            catch (Exception ex)
            {
                ExportError = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
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
            {
                for (var i = 0; i < QuestionCount; i++)
                {
                    dataService.SetAnswer(
                        roundId: round.Id,
                        teamId: row.TeamId,
                        questionIndex: i,
                        isCorrect: row.Answers[i].IsCorrect);
                }
            }

            Rebuild();
            IsEditing = false;
            OnSaved?.Invoke();
        }

        #endregion Private Methods
    }
}