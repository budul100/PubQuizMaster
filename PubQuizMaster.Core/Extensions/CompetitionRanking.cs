namespace PubQuizMaster.Core.Extensions
{
    /// <summary>
    /// Competition ranking: equal scores share a rank, the next rank skips accordingly (1, 1, 3).
    /// </summary>
    public static class CompetitionRanking
    {
        #region Public Methods

        /// <summary>
        /// Ranks all items as regular competitors. The sort is stable, so items with equal
        /// scores keep their input order. Pre-sort the input to define the tie order (e.g. by name).
        /// </summary>
        public static (T Item, int Rank)[] Rank<T>(this IEnumerable<T> items, Func<T, decimal> score)
            => items.Rank(
                score: score,
                isNonCompetitive: _ => false);

        /// <summary>
        /// Competition ranking with non-competitive support. One formula for every item:
        /// rank = 1 + number of regular items with a higher score.
        /// 1. Regular items are ranked solely among regular items (1, 1, 3).
        /// 2. Non-competitive items get the rank a regular item with the same score would have.
        /// 3. Ordering: rank ascending, regular before non-competitive, then input order.
        /// </summary>
        public static (T Item, int Rank)[] Rank<T>(this IEnumerable<T> items, Func<T, decimal> score,
            Func<T, bool> isNonCompetitive)
        {
            var entries = items
                .Select(item => (Item: item, Score: score(item), IsNonCompetitive: isNonCompetitive(item)))
                .ToArray();

            var regularScores = entries
                .Where(e => !e.IsNonCompetitive)
                .Select(e => e.Score)
                .ToArray();

            return [.. entries
                .Select(e => (e.Item, Rank: 1 + regularScores.Count(s => s > e.Score), e.IsNonCompetitive))
                .OrderBy(e => e.Rank)
                .ThenBy(e => e.IsNonCompetitive)
                .Select(e => (e.Item, e.Rank))];
        }

        #endregion Public Methods
    }
}