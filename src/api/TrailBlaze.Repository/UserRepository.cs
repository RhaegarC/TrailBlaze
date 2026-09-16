using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Repository
{
    public sealed class UserRepository(TrailBlazeContext context) : DatabaseRepository(context), IUserRepository
    {
        /// <summary>SQL Server's "Cannot insert duplicate key row in object ... with unique index".</summary>
        private const int DuplicateKeyInUniqueIndex = 2601;

        /// <summary>SQL Server's "Violation of PRIMARY KEY constraint".</summary>
        private const int DuplicatePrimaryKey = 2627;

        /// <inheritdoc/>
        public async Task<bool> AddIfAbsentAsync(User user, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);

            // Context, not the constructor's `context`: reading the base's field is what keeps
            // this type from capturing a second copy of the same context.
            Context.Add(user);

            try
            {
                await Context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsDuplicateKey(exception))
            {
                // Two replicas served the same first sign-in and both wrote. The primary key
                // picked one; this call lost, and losing is a benign outcome here rather than an
                // error -- the caller reads back the winner's row.
                //
                // Detaching matters more than it looks. A failed insert leaves the entity tracked
                // as Added, and EF resolves a later query by key through the change tracker, so
                // the caller's re-read would come back holding *these* unsaved values while
                // looking exactly like a row read from the database. Dropping it is what makes
                // the re-read actually report what is stored.
                Context.Entry(user).State = EntityState.Detached;
                return false;
            }
        }

        /// <summary>
        /// Whether this failure is a unique-key violation rather than a genuine write failure.
        /// </summary>
        /// <remarks>
        /// Only these two numbers are treated as "already there". Everything else — a dropped
        /// connection, a permission error, a value too long for its column — has a different
        /// cause and a different response, and swallowing it here would turn a diagnosable
        /// failure into a confusing "the row already existed".
        /// </remarks>
        private static bool IsDuplicateKey(DbUpdateException exception) =>
            exception.InnerException is SqlException sql
            && sql.Number is DuplicatePrimaryKey or DuplicateKeyInUniqueIndex;
    }
}
