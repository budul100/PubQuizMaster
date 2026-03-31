using CommunityToolkit.Mvvm.ComponentModel;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class TeamViewModel(Team team)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private string _name = team.Name;

        #endregion Private Fields

        #region Public Properties

        public Team Team { get; } = team;

        #endregion Public Properties
    }
}