using System;
using System.Collections.Generic;
using System.Linq;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class TeamAnswerRowViewModel(Guid teamId, string teamName,
        IReadOnlyList<AnswerCellViewModel> answers)
    {
        #region Public Properties

        public IReadOnlyList<AnswerCellViewModel> Answers { get; } = answers;

        public Guid TeamId { get; } = teamId;

        public string TeamName { get; } = teamName;

        public int Total => Answers.Count(a => a.IsCorrect == true);

        #endregion Public Properties
    }
}