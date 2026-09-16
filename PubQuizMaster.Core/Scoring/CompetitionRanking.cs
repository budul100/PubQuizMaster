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

        #endregion Public Methods
    }
}
