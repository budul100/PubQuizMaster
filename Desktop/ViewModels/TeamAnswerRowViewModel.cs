using System;
using System.Collections.Generic;
using System.Linq;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class TeamAnswerRowViewModel
    {
        public Guid TeamId { get; }
        public string TeamName { get; }
        public IReadOnlyList<AnswerCellViewModel> Answers { get; }
        public int Total => Answers.Count(a => a.IsCorrect == true);

        public TeamAnswerRowViewModel(Guid teamId, string teamName,
            IReadOnlyList<AnswerCellViewModel> answers)
        {
            TeamId = teamId;
            TeamName = teamName;
            Answers = answers;
        }
    }
}
