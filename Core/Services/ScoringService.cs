using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Services
{
    public class ScoringService(DataService dataService)
    {
        #region Private Fields

        private QuizSessionState? state;

        #endregion Private Fields

        #region Public Events

        public event Action<Answer>? AnswerRecorded
        {
            add { if (state != null) state.AnswerRecorded += value; }
            remove { if (state != null) state.AnswerRecorded -= value; }
        }

        #endregion Public Events

        #region Public Properties

        public Guid ActiveRoundId => state?.ActiveRoundId
            ?? throw new InvalidOperationException("QuizScoringService not initialized. Call Initialize() first.");

        #endregion Public Properties

        #region Public Methods

        public ScorerAssignment? GetAssignment(string scorerId)
        {
            if (state == null) return null;

            return dataService.GetAssignment(
                roundId: state.ActiveRoundId,
                scorerId: scorerId);
        }

        public void Initialize()
        {
            state = new QuizSessionState(dataService.QuizNight);
        }

        public Answer RecordAnswerBool(string scorerId, Guid teamId, int questionIndex, bool correct)
        {
            EnsureInitialized();

            return state!.RecordAnswer(
                roundId: state.ActiveRoundId,
                scorerId: scorerId,
                teamId: teamId,
                questionIndex: questionIndex,
                value: new AnswerBool { Correct = correct });
        }

        public Answer RecordAnswerPoint(string scorerId, Guid teamId, int questionIndex, decimal points)
        {
            EnsureInitialized();

            return state!.RecordAnswer(
                roundId: state.ActiveRoundId,
                scorerId: scorerId,
                teamId: teamId,
                questionIndex: questionIndex,
                value: new AnswerPoint { Points = points });
        }

        public void SetActiveRound(Guid roundId)
        {
            EnsureInitialized();
            state!.SetActiveRound(roundId);
        }

        #endregion Public Methods

        #region Private Methods

        private void EnsureInitialized()
        {
            if (state == null)
                throw new InvalidOperationException("QuizScoringService not initialized. Call Initialize() first.");
        }

        #endregion Private Methods
    }
}