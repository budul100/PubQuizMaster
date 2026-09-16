using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PubQuizMaster.Data
{
    public static class DbUpdateExceptionExtensions
    {
        #region Public Methods

        /// <summary>
        /// True if the update failed because the given unique index was violated.
        /// </summary>
        public static bool IsUniqueViolation(this DbUpdateException ex, string constraintName)
        {
            return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
                && pg.ConstraintName == constraintName;
        }

        #endregion Public Methods
    }
}
