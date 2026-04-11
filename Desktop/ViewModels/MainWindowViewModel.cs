using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Hub;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.Models;
using PubQuizMaster.Desktop.Web;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class MainWindowViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly KestrelHost kestrelHost;
        private readonly QuizNightService quizService;

        [ObservableProperty] private HostPhase phase = HostPhase.Review;
        [ObservableProperty] private string serverUrl = string.Empty;
        [ObservableProperty] private SetupViewModel setup = null!;

        #endregion Private Fields

        #region Public Constructors

        public MainWindowViewModel()
            : this(App.QuizNightService, App.KestrelHost)
        { }

        public MainWindowViewModel(QuizNightService quizService, KestrelHost kestrelHost)
        {
            this.quizService = quizService;
            this.kestrelHost = kestrelHost;

            QuizNightName = quizService.QuizNight.Name;

            var savedClient = App.SettingsService.Settings.ClientUrl;

            ServerUrl = string.IsNullOrWhiteSpace(savedClient)
                ? $"http://{KestrelHost.GetLocalIpAddress()}:{KestrelHost.Port}"
                : savedClient;

            kestrelHost.ServerReady += url => Dispatcher.UIThread.Post(() =>
            {
                if (string.IsNullOrWhiteSpace(App.SettingsService.Settings.ClientUrl))
                {
                    ServerUrl = url;
                }
            });

            kestrelHost.TunnelReady += url => Dispatcher.UIThread.Post(() =>
            {
                ServerUrl = url;

                var round = this.quizService.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == this.quizService.ActiveRoundId);
                if (round != null && Phase == HostPhase.Scoring)
                {
                    var (recorded, expected, _) = this.quizService.GetRoundProgress(round.Id);
                    ActiveRound.Refresh(round, recorded, expected, url);
                }
            });

            LeftPanel = new LeftPanelViewModel(quizService)
            {
                OnRoundSelected = OnRoundSelected,
                OnNewRound = ShowSetup
            };

            if (quizService.QuizNight.Rounds.Count > 0)
            {
                Setup = new SetupViewModel(quizService);

                LeftPanel.Refresh(
                    roundIsActive: false,
                    setupIsActive: false);

                var lastRound = quizService.QuizNight.Rounds.Last();
                LeftPanel.SelectEntry(lastRound.Id);

                var matrix = CreateMatrix(lastRound);
                Center.ShowMatrix(matrix);

                SwitchPhase(HostPhase.Review);
            }
            else
            {
                ShowSetup();
            }

            quizService.AnswerRecorded += OnAnswerRecorded;
        }

        #endregion Public Constructors

        #region Public Properties

        public ActiveRoundViewModel ActiveRound { get; } = new();

        public CenterViewModel Center { get; } = new();

        public bool IsReview => Phase == HostPhase.Review;

        public bool IsScoring => Phase == HostPhase.Scoring;

        public LeftPanelViewModel LeftPanel { get; private set; } = null!;

        public string? QuizNightName { get; }

        public bool ShowNewRoundButton => IsReview && !Center.IsSetupMode;

        public bool ShowStartButton => IsReview && Center.IsSetupMode;

        #endregion Public Properties

        #region Private Methods

        private RoundMatrixViewModel CreateMatrix(Round round)
        {
            var index = quizService.QuizNight.Rounds.IndexOf(round);
            var matrix = new RoundMatrixViewModel(
                nightService: quizService,
                round: round,
                roundNumber: index + 1);

            matrix.OnSaved = () => LeftPanel.Refresh(roundIsActive: false);
            matrix.OnDeleted = () =>
            {
                LeftPanel.Refresh(roundIsActive: false);
                ShowSetup();
            };

            return matrix;
        }

        [RelayCommand]
        private async Task FinalizeRound()
        {
            var round = quizService.QuizNight.Rounds
                .FirstOrDefault(r => r.Id == quizService.ActiveRoundId);
            if (round == null) return;

            quizService.FinalizeRound(round.Id);
            await quizService.SaveAsync();

            var (recorded, expected, _) = quizService.GetRoundProgress(round.Id);
            ActiveRound.Refresh(round, recorded, expected);

            var leaderboard = quizService.GetLeaderboards();

            if (kestrelHost?.HubContext != null)
            {
                await QuizHub.NotifyRoundFinalized(
                    hubContext: kestrelHost.HubContext,
                    roundId: round.Id,
                    leaderboard: leaderboard);
            }

            LeftPanel.Refresh(roundIsActive: false);
            LeftPanel.SelectEntry(round.Id);

            var matrix = CreateMatrix(round);

            Center.ShowMatrix(matrix);
            SwitchPhase(HostPhase.Review);
        }

        private void NotifyShowStartButton()
        {
            OnPropertyChanged(nameof(ShowStartButton));
            OnPropertyChanged(nameof(ShowNewRoundButton));
        }

        private void OnAnswerRecorded(Answer _)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var round = quizService.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == quizService.ActiveRoundId);
                if (round == null) return;

                var (recorded, expected, _) = quizService.GetRoundProgress(round.Id);
                ActiveRound.Refresh(round, recorded, expected, ServerUrl);
            });
        }

        private void OnRoundSelected(RoundEntryViewModel entry)
        {
            LeftPanel.SelectEntry(entry.RoundId);

            var round = quizService.QuizNight.Rounds.First(r => r.Id == entry.RoundId);
            var index = quizService.QuizNight.Rounds.IndexOf(round);

            var matrix = CreateMatrix(round);

            Center.ShowMatrix(matrix);
            NotifyShowStartButton();
        }

        [RelayCommand]
        private void OpenInBrowser()
        {
            if (string.IsNullOrEmpty(ServerUrl)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ServerUrl.Trim(),
                UseShellExecute = true
            });
        }

        private void ShowSetup()
        {
            Setup = new SetupViewModel(quizService);
            Center.ShowSetup(Setup);
            LeftPanel.ClearSelection();
            LeftPanel.Refresh(roundIsActive: false, setupIsActive: true);
            NotifyShowStartButton();
        }

        [RelayCommand]
        private async Task StartRound()
        {
            if (!Setup.CanStartRound) return;

            var allTeamIds = Setup.Teams.Select(t => t.Team.Id).ToList();
            var round = quizService.CreateRound(Setup.RoundName, Setup.QuestionCount, allTeamIds);

            foreach (var a in Setup.Assignments)
            {
                var ids = a.SelectedTeams.Select(t => t.Team.Id).ToList();
                if (ids.Count > 0)
                    quizService.AssignScorer(round.Id, a.ScorerId, a.Label, ids);
            }

            if (kestrelHost.HubContext != null)
                await QuizHub.NotifyRoundStarted(
                    kestrelHost.HubContext, round, quizService.QuizNight.MasterTeamList);

            var (recorded, expected, _) = quizService.GetRoundProgress(round.Id);
            ActiveRound.Initialize(round, recorded, expected, ServerUrl);

            LeftPanel.Refresh(roundIsActive: true);
            SwitchPhase(HostPhase.Scoring);
        }

        private void SwitchPhase(HostPhase phase)
        {
            Phase = phase;

            OnPropertyChanged(nameof(IsScoring));
            OnPropertyChanged(nameof(IsReview));

            NotifyShowStartButton();
        }

        #endregion Private Methods
    }
}