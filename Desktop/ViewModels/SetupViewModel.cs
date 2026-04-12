using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class SetupViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly HashSet<Guid> assignedTeamIds = [];
        private readonly DataService dataService;

        [ObservableProperty] private string newTeamName = string.Empty;
        [ObservableProperty] private int questionCount = 20;
        [ObservableProperty] private string roundName = string.Empty;
        [ObservableProperty] private string setupError = string.Empty;

        #endregion Private Fields

        #region Public Constructors

        public SetupViewModel(DataService dataService)
        {
            this.dataService = dataService;

            foreach (var team in dataService.QuizNight.MasterTeamList.OrderBy(t => t.Name))
            {
                Teams.Add(new TeamViewModel(team));
            }

            RoundName = $"Round {dataService.QuizNight.Rounds.Count + 1}";

            AddScorerInternal();
            AutoDistributeTeams();
        }

        #endregion Public Constructors

        #region Public Properties

        public ObservableCollection<ScorerAssignmentViewModel> Assignments { get; } = [];

        public bool CanStartRound
        {
            get
            {
                if (!Teams.Any() || !Assignments.Any())
                {
                    return false;
                }

                var allAssigned = Assignments.SelectMany(a => a.SelectedTeams).Select(t => t.Team.Id).ToList();
                if (allAssigned.Count != Teams.Count || allAssigned.Distinct().Count() != Teams.Count)
                {
                    return false;
                }

                if (dataService.QuizNight.Rounds
                    .Any(r => r.Name.Equals(RoundName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                var labels = Assignments.Select(a => a.Label.Trim()).ToList();
                if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != labels.Count)
                {
                    SetupError = "Scorer labels must be unique.";
                    return false;
                }

                if (SetupError == "Scorer labels must be unique.")
                {
                    SetupError = string.Empty;
                }

                return true;
            }
        }

        public ObservableCollection<TeamViewModel> Teams { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public void PrepareForNextRound()
        {
            RoundName = $"Round {dataService.QuizNight.Rounds.Count + 1}";
            SetupError = string.Empty;

            foreach (var a in Assignments)
            {
                a.ClearSelections();
            }

            assignedTeamIds.Clear();

            AutoDistributeTeams();
            NotifyStartButton();
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void AddScorer()
        {
            AddScorerInternal();
            AutoDistributeTeams();
            NotifyStartButton();
        }

        private void AddScorerInternal()
        {
            var n = Assignments.Count;
            var id = $"scorer-{(char)('a' + n)}";
            var label = $"Scorer {(char)('A' + n)}";

            var vm = new ScorerAssignmentViewModel(id, label, Teams, assignedTeamIds)
            {
                OnAssignmentChanged = OnScorerAssignmentChanged,
                LabelEdited = NotifyStartButton
            };

            Assignments.Add(vm);
        }

        [RelayCommand]
        private void AddTeam()
        {
            var name = NewTeamName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (Teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                SetupError = $"Team \"{name}\" already exists.";
                return;
            }

            SetupError = string.Empty;

            var team = dataService.AddTeam(name);
            var vm = new TeamViewModel(team);

            NewTeamName = string.Empty;

            // Insert at correct alphabetical position — single event, no Clear
            var insertIndex = 0;
            while (insertIndex < Teams.Count &&
                   string.Compare(Teams[insertIndex].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }

            Teams.Insert(insertIndex, vm);

            dataService
                .ReorderTeams(Teams.Select(t => t.Team.Id).ToList());

            AutoDistributeTeams();
            NotifyStartButton();
        }

        private void AutoDistributeTeams()
        {
            if (!Assignments.Any() || !Teams.Any())
            {
                return;
            }

            assignedTeamIds.Clear();
            foreach (var a in Assignments)
            {
                a.ClearSelections();
            }

            var teams = Teams.ToList();
            for (int i = 0; i < teams.Count; i++)
            {
                var scorer = Assignments[i % Assignments.Count];
                var slot = scorer.Teams.FirstOrDefault(t => t.TeamVm.Team.Id == teams[i].Team.Id);
                if (slot != null)
                {
                    slot.IsAssigned = true;
                }

                assignedTeamIds.Add(teams[i].Team.Id);
            }

            foreach (var a in Assignments)
            {
                a.UpdateDisabledStates();
            }
        }

        private void NotifyStartButton() => OnPropertyChanged(nameof(CanStartRound));

        partial void OnRoundNameChanged(string value)
        {
            if (dataService.QuizNight.Rounds.Any(r => r.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                SetupError = $"Round \"{value.Trim()}\" already exists.";
            }
            else if (SetupError.StartsWith("Round"))
            {
                SetupError = string.Empty;
            }

            NotifyStartButton();
        }

        private void OnScorerAssignmentChanged(Guid teamId, bool assigned)
        {
            if (assigned)
            {
                assignedTeamIds.Add(teamId);
            }
            else
            {
                assignedTeamIds.Remove(teamId);
            }

            foreach (var a in Assignments)
            {
                a.UpdateDisabledStates();
            }

            NotifyStartButton();
        }

        [RelayCommand]
        private void RemoveScorer(ScorerAssignmentViewModel vm)
        {
            Assignments.Remove(vm);
            foreach (var t in vm.Teams.Where(t => t.IsAssigned))
            {
                assignedTeamIds.Remove(t.TeamVm.Team.Id);
            }

            foreach (var a in Assignments)
            {
                a.UpdateDisabledStates();
            }

            AutoDistributeTeams();
            NotifyStartButton();
        }

        [RelayCommand]
        private void RemoveTeam(TeamViewModel vm)
        {
            Teams.Remove(vm);

            dataService.QuizNight.MasterTeamList.Remove(vm.Team);
            assignedTeamIds.Remove(vm.Team.Id);

            NotifyStartButton();
        }

        #endregion Private Methods
    }
}