using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Interface.Repository
{
    public interface IUserRepository : IDbRepository
    {
        /// <summary>
        /// Inserts a user, treating a row that already holds the same id as success rather than
        /// as a failure.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This exists because <c>GetOrCreateAsync</c> cannot be atomic across the several
        /// replicas the API runs on. Two of them can serve the same person's first sign-in at the
        /// same moment, both find no row, and both try to write one; the primary key means exactly
        /// one of them wins. That race is the normal case at deploy time, not an exotic one, so
        /// the loser must not surface as a 500.
        /// </para>
        /// <para>
        /// Recognising the duplicate is data-access knowledge — it depends on the provider and on
        /// which index was violated — which is why the catch lives in the repository rather than
        /// in the service that calls it.
        /// </para>
        /// </remarks>
        /// <param name="user">The row to insert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> when this call inserted the row; <c>false</c> when a row with the
        /// same id already existed. The caller must re-read on <c>false</c> — the row it gets
        /// back is the other writer's, not this one's.</returns>
        Task<bool> AddIfAbsentAsync(User user, CancellationToken cancellationToken = default);
    }
}
