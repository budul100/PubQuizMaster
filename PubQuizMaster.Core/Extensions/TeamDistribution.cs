namespace PubQuizMaster.Core.Extensions
{
    /// <summary>
    /// Strictly alphabetical distribution of teams across scorer stations.
    /// Every station gets a contiguous block of names, stations in label order.
    /// </summary>
    public static class TeamDistribution
    {
        #region Public Methods

        /// <summary>
        /// Index of the station whose alphabetical range covers the name: the first station
        /// (in label order) whose last name is not before it. Names beyond every range go to
        /// the last station with teams. Empty stations are skipped unless all stations are empty.
        /// </summary>
        /// <param name="stationNames">Team names per station, stations in label order.</param>
        public static int FindStation(IReadOnlyList<string[]> stationNames, string name)
        {
            if (stationNames.Count == 0)
            {
                throw new ArgumentException("At least one station is required.", nameof(stationNames));
            }

            var comparer = TeamNameComparer.Instance;
            var lastFilled = -1;

            for (var i = 0; i < stationNames.Count; i++)
            {
                if (stationNames[i].Length == 0) continue;

                lastFilled = i;

                var lastName = stationNames[i].Max(comparer)!;
                if (comparer.Compare(name, lastName) <= 0)
                {
                    return i;
                }
            }

            return lastFilled >= 0 ? lastFilled : 0;
        }

        /// <summary>
        /// Sorts the items by name and splits them into one contiguous block per station.
        /// Blocks differ by at most one item, the first stations take the remainder.
        /// </summary>
        public static T[][] Split<T>(IEnumerable<T> items, Func<T, string> name, int stationCount)
        {
            if (stationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stationCount), "At least one station is required.");
            }

            var sorted = items
                .OrderBy(name, TeamNameComparer.Instance)
                .ToArray();

            var baseSize = sorted.Length / stationCount;
            var remainder = sorted.Length % stationCount;

            var blocks = new T[stationCount][];
            var offset = 0;

            for (var i = 0; i < stationCount; i++)
            {
                var size = baseSize + (i < remainder ? 1 : 0);
                blocks[i] = sorted[offset..(offset + size)];
                offset += size;
            }

            return blocks;
        }

        #endregion Public Methods
    }
}
