using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Web.Helpers
{
    /// <summary>
    /// Sample round for the practice mode of the scorer page. Lives only in the page,
    /// nothing is stored and the dashboard only sees the station as practicing.
    /// </summary>
    public static class PracticeRound
    {
        #region Public Properties

        public static Round Content { get; } = new()
        {
            Position = 0,
            Questions =
            [
                new Question { Position = 1, Category = "Geography", Text = "What is the capital of Australia?", Answer = "Canberra" },
                new Question { Position = 2, Category = "Science", Text = "Which planet is known as the red planet?", Answer = "Mars" },
                new Question { Position = 3, Category = "Music", Text = "How many strings does a standard guitar have?", Answer = "6" },
                new Question { Position = 4, Category = "History", Text = "In which year did the Berlin Wall fall?", Answer = "1989" },
                new Question { Position = 5, Category = "Sports", Text = "How many players does a football team have on the pitch?", Answer = "11" },
            ]
        };

        #endregion Public Properties

        #region Public Methods

        public static Core.Models.Event.Round CreateRound() => new()
        {
            Id = Guid.Empty,
            Name = "Practice",
            Length = Content.Questions.Count
        };

        /// <summary>
        /// The station's own preliminary teams, so the scorer also practices sorting the real stack.
        /// Fictional teams if the station has none yet.
        /// </summary>
        public static List<Team> CreateTeams(IEnumerable<Team>? previewTeams)
        {
            var teams = previewTeams?.ToList() ?? [];

            return teams.Count > 0
                ? teams
                :
                [
                    new Team { Name = "Practice Team Alpha" },
                    new Team { Name = "Practice Team Bravo" },
                    new Team { Name = "Practice Team Charlie" },
                    new Team { Name = "Practice Team Delta" },
                ];
        }

        #endregion Public Methods
    }
}
