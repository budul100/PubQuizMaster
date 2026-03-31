using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class AssignableTeamViewModel(TeamViewModel teamVm, bool isAssigned = false)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool _isAssigned = isAssigned;
        [ObservableProperty] private bool _isDisabled;

        #endregion Private Fields

        #region Public Properties

        public Action<Guid, bool>? AssignmentChanged { get; set; }

        public string Name => TeamVm.Name;

        public Guid TeamId => TeamVm.Team.Id;

        public TeamViewModel TeamVm { get; } = teamVm;

        #endregion Public Properties

        #region Private Methods

        partial void OnIsAssignedChanged(bool value) =>
            AssignmentChanged?.Invoke(TeamVm.Team.Id, value);

        #endregion Private Methods
    }
}