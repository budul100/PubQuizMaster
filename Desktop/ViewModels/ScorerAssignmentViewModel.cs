using CommunityToolkit.Mvvm.ComponentModel;
using PubQuizMaster.Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class ScorerAssignmentViewModel : ViewModelBase
    {
        [ObservableProperty] private string _scorerId;
        [ObservableProperty] private string _label;

        private readonly ObservableCollection<TeamViewModel> _masterTeams;
        private readonly HashSet<Guid> _globalAssigned;

        public ObservableCollection<AssignableTeamViewModel> Teams { get; } = new();
        public IEnumerable<TeamViewModel> SelectedTeams =>
            Teams.Where(t => t.IsAssigned).Select(t => t.TeamVm);

        public Action<Guid, bool>? OnAssignmentChanged { get; set; }

        public ScorerAssignmentViewModel(string scorerId, string label,
            ObservableCollection<TeamViewModel> masterTeams,
            HashSet<Guid> globalAssigned)
        {
            _scorerId = scorerId;
            _label = label;
            _masterTeams = masterTeams;
            _globalAssigned = globalAssigned;

            foreach (var t in masterTeams)
                AddSlot(t, false);

            _masterTeams.CollectionChanged += OnMasterTeamsChanged;
        }

        private void OnMasterTeamsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TeamViewModel t in e.NewItems)
                    AddSlot(t, false);

            if (e.OldItems != null)
                foreach (TeamViewModel t in e.OldItems)
                {
                    var slot = Teams.FirstOrDefault(s => s.TeamVm.Team.Id == t.Team.Id);
                    if (slot != null)
                    {
                        if (slot.IsAssigned)
                            OnAssignmentChanged?.Invoke(t.Team.Id, false);
                        Teams.Remove(slot);
                    }
                }

            UpdateDisabledStates();
        }

        private void AddSlot(TeamViewModel t, bool isAssigned)
        {
            var slot = new AssignableTeamViewModel(t, isAssigned);
            slot.AssignmentChanged = (id, assigned) => OnAssignmentChanged?.Invoke(id, assigned);
            Teams.Add(slot);
        }

        public void UpdateDisabledStates()
        {
            foreach (var t in Teams)
                t.IsDisabled = !t.IsAssigned && _globalAssigned.Contains(t.TeamVm.Team.Id);
        }

        public void ClearSelections()
        {
            foreach (var t in Teams.Where(t => t.IsAssigned))
                t.IsAssigned = false;
        }

        public void SetAssigned(Guid teamId, bool value)
        {
            var slot = Teams.FirstOrDefault(t => t.TeamVm.Team.Id == teamId);
            if (slot != null) slot.IsAssigned = value;
        }
    }
}
