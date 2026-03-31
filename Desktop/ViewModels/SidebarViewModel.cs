// ===== SidebarViewModel.cs =====
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;
using PubQuizMaster.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class SidebarViewModel : ViewModelBase
    {
        private readonly QuizNightService _svc;

        public ObservableCollection<LeaderboardEntryViewModel> LastRoundBoard { get; } = new();
        public ObservableCollection<LeaderboardEntryViewModel> TotalBoard { get; } = new();
        public ObservableCollection<RoundMatrixViewModel> AllRounds { get; } = new();

        public string LastRoundLabel { get; private set; } = "This Round";
        public bool HasResults { get; private set; } = false;

        public SidebarViewModel(QuizNightService svc)
        {
            _svc = svc;
        }

        public void Update(Round round, List<LeaderboardEntry> total)
        {
            LastRoundLabel = round.Name;
            OnPropertyChanged(nameof(LastRoundLabel));

            LastRoundBoard.Clear();
            var rank = 1;
            foreach (var x in _svc.QuizNight.MasterTeamList
                .Select(t => new { t, Score = round.GetTeamScore(t.Id) })
                .Where(x => x.Score.HasValue)
                .OrderByDescending(x => x.Score))
            {
                LastRoundBoard.Add(new LeaderboardEntryViewModel
                { Rank = rank++, TeamName = x.t.Name, Score = x.Score!.Value });
            }

            TotalBoard.Clear();
            rank = 1;
            foreach (var e in total)
                TotalBoard.Add(new LeaderboardEntryViewModel
                { Rank = rank++, TeamName = e.Team.Name, Score = e.TotalScore });

            var existing = AllRounds.FirstOrDefault(r => r.RoundName == round.Name);
            if (existing != null)
                existing.Rebuild();
            else
                AllRounds.Add(new RoundMatrixViewModel(round, AllRounds.Count + 1, _svc));

            HasResults = true;
            OnPropertyChanged(nameof(HasResults));
        }
    }
}
