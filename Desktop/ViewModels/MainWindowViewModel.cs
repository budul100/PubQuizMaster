// ===== MainWindowViewModel.cs (nach Refactoring) =====
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.ViewModels;
using PubQuizMaster.Desktop.Web;
using System.Linq;
using System.Threading.Tasks;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly QuizNightService _svc;
        private readonly KestrelHost _kestrel;

        [ObservableProperty] private HostPhase _phase = HostPhase.Setup;
        [ObservableProperty] private string _serverUrl = string.Empty;

        public bool IsSetup => Phase == HostPhase.Setup;
        public bool IsActiveRound => Phase == HostPhase.ActiveRound;
        public bool IsResults => Phase == HostPhase.Results;

        public string QuizNightName { get; }

        // Sub-ViewModels
        public SetupViewModel Setup { get; }
        public ActiveRoundViewModel ActiveRound { get; }
        public SidebarViewModel Sidebar { get; }

        // Designer constructor
        public MainWindowViewModel() { }

        public MainWindowViewModel(QuizNightService svc, KestrelHost kestrel)
        {
            _svc = svc;
            _kestrel = kestrel;

            QuizNightName = svc.QuizNight.Name;
            ServerUrl = $"http://{KestrelHost.GetLocalIpAddress()}:{KestrelHost.Port}";

            Setup = new SetupViewModel(svc);
            ActiveRound = new ActiveRoundViewModel();
            Sidebar = new SidebarViewModel(svc);

            svc.AnswerRecorded += OnAnswerRecorded;
            kestrel.ServerReady += url => ServerUrl = url;
        }

        [RelayCommand]
        private async Task StartRound()
        {
            if (!Setup.CanStartRound) return;

            var allTeamIds = Setup.Teams.Select(t => t.Team.Id).ToList();
            var round = _svc.CreateRound(Setup.RoundName, Setup.QuestionCount, allTeamIds);

            foreach (var a in Setup.Assignments)
            {
                var ids = a.SelectedTeams.Select(t => t.Team.Id).ToList();
                if (ids.Any())
                    _svc.AssignScorer(round.Id, a.ScorerId, a.Label, ids);
            }

            if (_kestrel.HubContext != null)
                await QuizHub.NotifyRoundStarted(
                    _kestrel.HubContext, round, _svc.QuizNight.MasterTeamList);

            var (recorded, expected, _) = _svc.GetRoundProgress(round.Id);
            ActiveRound.Initialize(round, recorded, expected);

            SwitchPhase(HostPhase.ActiveRound);
        }

        [RelayCommand]
        private async Task FinalizeRound()
        {
            var round = _svc.QuizNight.Rounds
                .FirstOrDefault(r => r.Id == _svc.ActiveRoundId);
            if (round == null) return;

            _svc.FinalizeRound(round.Id);
            var leaderboard = _svc.GetLeaderboard();

            if (_kestrel.HubContext != null)
                await QuizHub.NotifyRoundFinalized(_kestrel.HubContext, round.Id, leaderboard);

            Sidebar.Update(round, leaderboard);
            SwitchPhase(HostPhase.Results);
        }

        [RelayCommand]
        private void NextRound()
        {
            Setup.PrepareForNextRound();
            SwitchPhase(HostPhase.Setup);
        }

        private void OnAnswerRecorded(Answer _)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var round = _svc.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == _svc.ActiveRoundId);
                if (round == null) return;

                var (recorded, expected, _) = _svc.GetRoundProgress(round.Id);
                ActiveRound.Refresh(round, recorded, expected);
                Sidebar.Update(round, _svc.GetLeaderboard());
            });
        }

        private void SwitchPhase(HostPhase phase)
        {
            Phase = phase;
            OnPropertyChanged(nameof(IsSetup));
            OnPropertyChanged(nameof(IsActiveRound));
            OnPropertyChanged(nameof(IsResults));
        }
    }
}
