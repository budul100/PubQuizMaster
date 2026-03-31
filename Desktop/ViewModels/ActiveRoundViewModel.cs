// ===== ActiveRoundViewModel.cs =====
using System.Collections.ObjectModel;
using System.Linq;
using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class ActiveRoundViewModel : ViewModelBase
    {
        #region Public Properties

        public string ActiveRoundName { get; private set; } = string.Empty;
        public string AnswerProgress => $"{AnswersRecorded} / {AnswersExpected}";
        public int AnswersExpected { get; private set; }
        public int AnswersRecorded { get; private set; }
        public ObservableCollection<ScorerStatusViewModel> ScorerStatuses { get; } = new();

        #endregion Public Properties

        #region Public Methods

        public void Initialize(Round round, int recorded, int expected)
        {
            ActiveRoundName = round.Name;
            Refresh(round, recorded, expected);

            OnPropertyChanged(nameof(ActiveRoundName));
        }

        public void Refresh(Round round, int recorded, int expected)
        {
            AnswersRecorded = recorded;
            AnswersExpected = expected;
            OnPropertyChanged(nameof(AnswersRecorded));
            OnPropertyChanged(nameof(AnswersExpected));
            OnPropertyChanged(nameof(AnswerProgress));

            ScorerStatuses.Clear();
            foreach (var a in round.Assignments)
            {
                var answered = round.Answers.Count(ans => a.TeamIds.Contains(ans.TeamId));
                ScorerStatuses.Add(new ScorerStatusViewModel
                {
                    ScorerId = a.ScorerId,
                    Label = a.Label,
                    Answered = answered,
                    Expected = a.TeamIds.Count * round.QuestionCount,
                });
            }
        }

        #endregion Public Methods
    }
}