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

        private readonly DataService dataService;
        private readonly KestrelHost kestrelHost;
        private readonly ScoringService scoringService;

        [ObservableProperty] private RoundMatrixViewModel matrix = null!;
        [ObservableProperty] private HostPhase phase = HostPhase.Review;
        [ObservableProperty] private string serverUrl = string.Empty;
        [ObservableProperty] private SetupViewModel setup = null!;

        #endregion Private Fields

        #region Public Constructors

        public MainWindowViewModel()
        {
            this.dataService = App.DataService;
            this.scoringService = App.ScoringService;
            this.kestrelHost = App.KestrelHost;

            QuizNightName = dataService.QuizNight.Name;

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

                var round = this.dataService.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == this.scoringService.ActiveRoundId);
                if (round != null && Phase == HostPhase.Scoring)
                {
                    var (recorded, expected, _) = this.dataService.GetRoundProgress(round.Id);
                    ActiveRound.Refresh(round, recorded, expected, url);
                }
            });

            LeftPanel = new LeftPanelViewModel(dataService)
            {
                OnRoundSelected = OnRoundSelected,
                OnNewRound = ShowSetup
            };

            if (dataService.QuizNight.Rounds.Count > 0)
            {
                Setup = new SetupViewModel(dataService);

                LeftPanel.Refresh(
                    roundIsActive: false,
                    setupIsActive: false);

                var lastRound = dataService.QuizNight.Rounds.Last();
                LeftPanel.SelectEntry(lastRound.Id);

                CreateMatrix(lastRound);
                SwitchPhase(HostPhase.Review);
            }
            else
            {
                ShowSetup();
            }

            scoringService.AnswerRecorded += OnAnswerRecorded;
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

        private void CreateMatrix(Round round)
        {
            var index = dataService.QuizNight.Rounds.IndexOf(round);

            Matrix = new RoundMatrixViewModel(
                dataService: dataService,
                round: round,
                roundNumber: index + 1);

            Matrix.OnSaved = () => LeftPanel.Refresh(roundIsActive: false);
            Matrix.OnDeleted = () =>
            {
                LeftPanel.Refresh(roundIsActive: false);
                ShowSetup();
            };

            Center.ShowMatrix(Matrix);
        }

        [RelayCommand]
        private async Task FinalizeRound()
        {
            var round = dataService.QuizNight.Rounds
                .FirstOrDefault(r => r.Id == scoringService.ActiveRoundId);

            if (round == null)
            {
                return;
            }

            dataService.FinalizeRound(round.Id);
            await dataService.SaveAsync();

            var (recorded, expected, _) = dataService.GetRoundProgress(round.Id);
            ActiveRound.Refresh(round, recorded, expected);

            var leaderboard = dataService.GetLeaderboards();

            if (kestrelHost.HubContext != null)
            {
                await QuizHub.NotifyRoundFinalized(
                    hubContext: kestrelHost.HubContext,
                    roundId: round.Id,
                    leaderboard: leaderboard);
            }

            LeftPanel.Refresh(roundIsActive: false);
            LeftPanel.SelectEntry(round.Id);

            CreateMatrix(round);
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
                var round = dataService.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == scoringService.ActiveRoundId);
                if (round == null)
                {
                    return;
                }

                var (recorded, expected, _) = dataService.GetRoundProgress(round.Id);
                ActiveRound.Refresh(round, recorded, expected, ServerUrl);
            });
        }

        private void OnRoundSelected(RoundEntryViewModel entry)
        {
            LeftPanel.SelectEntry(entry.RoundId);

            var round = dataService.QuizNight.Rounds.First(r => r.Id == entry.RoundId);
            CreateMatrix(round);

            NotifyShowStartButton();
        }

        [RelayCommand]
        private void OpenInBrowser()
        {
            if (string.IsNullOrEmpty(ServerUrl))
            {
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ServerUrl.Trim(),
                UseShellExecute = true
            });
        }

        private void ShowSetup()
        {
            Setup = new SetupViewModel(dataService);

            Center.ShowSetup(Setup);

            LeftPanel.ClearSelection();
            LeftPanel.Refresh(
                roundIsActive: false,
                setupIsActive: true);

            NotifyShowStartButton();
        }

        [RelayCommand]
        private async Task StartRound()
        {
            if (!Setup.CanStartRound)
            {
                return;
            }

            var allTeamIds = Setup.Teams.Select(t => t.Team.Id).ToList();

            var round = dataService.CreateRound(
                name: Setup.RoundName,
                questionCount: Setup.QuestionCount,
                activeTeamIds: allTeamIds);

            scoringService.SetActiveRound(round.Id);

            foreach (var assignement in Setup.Assignments)
            {
                var ids = assignement.SelectedTeams.Select(t => t.Team.Id).ToList();

                if (ids.Count > 0)
                {
                    dataService.AssignScorer(
                        roundId: round.Id,
                        scorerId: assignement.ScorerId,
                        label: assignement.Label,
                        teamIds: ids);
                }
            }

            if (kestrelHost.HubContext != null)
            {
                await QuizHub.NotifyRoundStarted(
                    kestrelHost.HubContext, round, dataService.QuizNight.MasterTeamList);
            }

            var (recorded, expected, _) = dataService.GetRoundProgress(round.Id);
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