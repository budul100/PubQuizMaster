namespace PubQuizMaster.Core.Scoring
{
    /// <summary>
    /// Competition ranking: equal scores share a rank, the next rank skips accordingly (1, 1, 3).
    /// </summary>
    public static class CompetitionRanking
    {
        #region Public Methods

        /// <summary>
        /// Orders by score descending and assigns the ranks. The sort is stable, so items with equal
        /// scores keep their input order. Pre-sort the input to define the tie order (e.g. by name).
        /// </summary>
        public static (T Item, int Rank)[] Rank<T>(IEnumerable<T> items, Func<T, decimal> score)
        {
            var ordered = items.OrderByDescending(score).ToArray();
            var ranked = new (T Item, int Rank)[ordered.Length];
            var rank = 1;

            for (var i = 0; i < ordered.Length; i++)
            {
                if (i > 0 && score(ordered[i]) != score(ordered[i - 1]))
                {
                    rank = i + 1;
                }

                ranked[i] = (ordered[i], rank);
            }

            return ranked;
        }

        /// <summary>
        /// Competition ranking with non-competitive (AK) support:
        /// 1. Regular ranks are computed solely among regular teams (1, 1, 3).
        /// 2. Non-competitive teams receive the rank: 1 + count of regular teams with higher score.
        /// 3. Ordering: Rank ascending, regular teams before non-competitive teams, preserving pre-sort.
        /// </summary>
        public static (T Item, int Rank)[] Rank<T>(
            IEnumerable<T> items,
            Func<T, decimal> score,
            Func<T, bool> isNonCompetitive)
        {
            var inputArray = items.ToArray();
            if (inputArray.Length == 0) return [];

            var regularTeams = inputArray.Where(x => !isNonCompetitive(x)).ToArray();
            var regularRanked = Rank(regularTeams, score);

            var rankedResult = new (T Item, int Rank)[inputArray.Length];
            var index = 0;

            foreach (var item in inputArray)
            {
                if (!isNonCompetitive(item))
                {
                    var match = regularRanked.First(r => EqualityComparer<T>.Default.Equals(r.Item, item));
                    rankedResult[index++] = (item, match.Rank);
                }
                else
                {
                    var itemScore = score(item);
                    var higherRegularCount = regularTeams.Count(r => score(r) > itemScore);
                    var akRank = 1 + higherRegularCount;
                    rankedResult[index++] = (item, akRank);
                }
            }

            // Ordering: Rank asc, regular teams first, stable relative order preserved
            return [.. rankedResult
                .OrderBy(r => r.Rank)
                .ThenBy(r => isNonCompetitive(r.Item) ? 1 : 0)];
        }

        #endregion Public Methods
    }
}