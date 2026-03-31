using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class ScorerAssignmentViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly HashSet<Guid> _globalAssigned;
        private readonly ObservableCollection<TeamViewModel> _masterTeams;
        [ObservableProperty] private string _label;
        [ObservableProperty] private string _scorerId;

        #endregion Private Fields

        #region Public Constructors

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

        #endregion Public Constructors

        #region Public Properties

        public Action<Guid, bool>? OnAssignmentChanged { get; set; }

        public IEnumerable<TeamViewModel> SelectedTeams => Teams
            .Where(t => t.IsAssigned)
            .Select(t => t.TeamVm);

        public ObservableCollection<AssignableTeamViewModel> Teams { get; } = [];

        #endregion Public Properties

        #region Public Methods

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

        public void UpdateDisabledStates()
        {
            foreach (var t in Teams)
                t.IsDisabled = !t.IsAssigned && _globalAssigned.Contains(t.TeamVm.Team.Id);
        }

        #endregion Public Methods

        #region Private Methods

        private void AddSlot(TeamViewModel t, bool isAssigned)
        {
            var slot = new AssignableTeamViewModel(t, isAssigned)
            {
                AssignmentChanged = (id, assigned) => OnAssignmentChanged?.Invoke(id, assigned)
            };
            Teams.Add(slot);
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

        #endregion Private Methods
    }
}