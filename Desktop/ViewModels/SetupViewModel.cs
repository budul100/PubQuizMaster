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

        private readonly HashSet<Guid> _assignedTeamIds = [];
        private readonly QuizNightService _svc;

        [ObservableProperty] private string _newTeamName = string.Empty;
        [ObservableProperty] private int _questionCount = 6;
        [ObservableProperty] private string _roundName = string.Empty;
        [ObservableProperty] private string _setupError = string.Empty;

        #endregion Private Fields

        #region Public Constructors

        public SetupViewModel(QuizNightService svc)
        {
            _svc = svc;

            foreach (var t in svc.QuizNight.MasterTeamList.OrderBy(t => t.Name))
                Teams.Add(new TeamViewModel(t));

            RoundName = $"Round {svc.QuizNight.Rounds.Count + 1}";

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
                if (!Teams.Any() || !Assignments.Any()) return false;
                var allAssigned = Assignments.SelectMany(a => a.SelectedTeams).Select(t => t.Team.Id).ToList();
                return allAssigned.Count == Teams.Count &&
                       allAssigned.Distinct().Count() == Teams.Count;
            }
        }

        public ObservableCollection<TeamViewModel> Teams { get; } = [];

        #endregion Public Properties

        #region Public Methods

        // Called by MainWindowViewModel after FinalizeRound → NextRound
        public void PrepareForNextRound()
        {
            RoundName = $"Round {_svc.QuizNight.Rounds.Count + 1}";
            QuestionCount = 6;
            SetupError = string.Empty;

            foreach (var a in Assignments)
                a.ClearSelections();
            _assignedTeamIds.Clear();

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

            var vm = new ScorerAssignmentViewModel(id, label, Teams, _assignedTeamIds)
            {
                OnAssignmentChanged = OnScorerAssignmentChanged
            };

            Assignments.Add(vm);
        }

        [RelayCommand]
        private void AddTeam()
        {
            var name = NewTeamName.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            if (Teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                SetupError = $"Team \"{name}\" already exists.";
                return;
            }

            SetupError = string.Empty;
            var team = _svc.AddTeam(name);
            Teams.Add(new TeamViewModel(team));
            NewTeamName = string.Empty;

            AutoDistributeTeams();
            NotifyStartButton();
        }

        private void AutoDistributeTeams()
        {
            if (!Assignments.Any() || !Teams.Any()) return;
            _assignedTeamIds.Clear();
            foreach (var a in Assignments) a.ClearSelections();

            var teams = Teams.ToList();
            for (int i = 0; i < teams.Count; i++)
            {
                var scorer = Assignments[i % Assignments.Count];
                var slot = scorer.Teams.FirstOrDefault(t => t.TeamVm.Team.Id == teams[i].Team.Id);
                if (slot != null) slot.IsAssigned = true;
                _assignedTeamIds.Add(teams[i].Team.Id);
            }

            foreach (var a in Assignments)
                a.UpdateDisabledStates();
        }

        private void NotifyStartButton() => OnPropertyChanged(nameof(CanStartRound));

        private void OnScorerAssignmentChanged(Guid teamId, bool assigned)
        {
            if (assigned) _assignedTeamIds.Add(teamId);
            else _assignedTeamIds.Remove(teamId);
            foreach (var a in Assignments)
                a.UpdateDisabledStates();
            NotifyStartButton();
        }

        [RelayCommand]
        private void RemoveScorer(ScorerAssignmentViewModel vm)
        {
            Assignments.Remove(vm);
            foreach (var t in vm.Teams.Where(t => t.IsAssigned))
                _assignedTeamIds.Remove(t.TeamVm.Team.Id);
            foreach (var a in Assignments)
                a.UpdateDisabledStates();
            AutoDistributeTeams();
            NotifyStartButton();
        }

        [RelayCommand]
        private void RemoveTeam(TeamViewModel vm)
        {
            Teams.Remove(vm);
            _svc.QuizNight.MasterTeamList.Remove(vm.Team);
            _assignedTeamIds.Remove(vm.Team.Id);
            NotifyStartButton();
        }

        [RelayCommand]
        private void SortTeams()
        {
            var sorted = Teams.OrderBy(t => t.Name).ToList();
            Teams.Clear();
            foreach (var t in sorted) Teams.Add(t);
            _svc.ReorderTeams(Teams.Select(t => t.Team.Id).ToList());
        }

        #endregion Private Methods
    }
}