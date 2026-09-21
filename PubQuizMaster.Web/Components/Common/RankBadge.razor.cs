using Microsoft.AspNetCore.Components;

namespace PubQuizMaster.Web.Components.Common
{
    /// <summary>
    /// Podium badge for a competition rank. Ranks beyond MaxRank and teams out of competition
    /// either fall back to the plain rank number or render nothing at all.
    /// </summary>
    public partial class RankBadge
    {
        #region Private Fields

        /// <summary>Hard ceiling: only gold, silver and bronze exist, whatever MaxRank says.</summary>
        private const int PodiumSize = 3;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        /// Teams out of competition carry a shadow rank from the ranking extension.
        /// Set this to false for them so they never show a trophy.
        /// </summary>
        [Parameter] public bool IsCompeting { get; set; } = true;

        /// <summary>Highest rank that still gets a badge. 1 shows the winner only, 3 the full podium.</summary>
        [Parameter] public int MaxRank { get; set; } = PodiumSize;

        /// <summary>Competition rank, 1-based. 0 means "not ranked" and renders nothing.</summary>
        [Parameter] public int Rank { get; set; }

        /// <summary>
        /// Renders the plain rank number where no badge applies. Off where the badge only
        /// decorates a value that is printed next to it anyway.
        /// </summary>
        [Parameter] public bool ShowPlainRank { get; set; }

        #endregion Public Properties

        #region Private Properties

        private string BadgeClass => Rank switch
        {
            1 => "bg-warning text-dark",

            2 => "bg-secondary text-white",

            _ => "bg-danger text-white"
        };

        private string BadgeTitle => Rank switch
        {
            1 => "1st place",

            2 => "2nd place",

            _ => "3rd place"
        };

        private bool HasBadge => IsCompeting
            && Rank >= 1
            && Rank <= Math.Min(MaxRank, PodiumSize);

        #endregion Private Properties
    }
}