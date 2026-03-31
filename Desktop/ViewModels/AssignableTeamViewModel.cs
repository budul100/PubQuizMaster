using CommunityToolkit.Mvvm.ComponentModel;
using PubQuizMaster.Desktop.ViewModels;
using System;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class AssignableTeamViewModel : ViewModelBase
    {
        public TeamViewModel TeamVm { get; }
        public Guid TeamId => TeamVm.Team.Id;
        public string Name => TeamVm.Name;

        [ObservableProperty] private bool _isAssigned;
        [ObservableProperty] private bool _isDisabled;

        public Action<Guid, bool>? AssignmentChanged { get; set; }

        public AssignableTeamViewModel(TeamViewModel teamVm, bool isAssigned = false)
        {
            TeamVm = teamVm;
            _isAssigned = isAssigned;
        }

        partial void OnIsAssignedChanged(bool value) =>
            AssignmentChanged?.Invoke(TeamVm.Team.Id, value);
    }
}
