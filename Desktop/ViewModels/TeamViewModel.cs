using CommunityToolkit.Mvvm.ComponentModel;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class TeamViewModel : ViewModelBase
    {
        public Team Team { get; }

        [ObservableProperty]
        private string _name;

        public TeamViewModel(Team team)
        {
            Team = team;
            _name = team.Name;
        }
    }
}
