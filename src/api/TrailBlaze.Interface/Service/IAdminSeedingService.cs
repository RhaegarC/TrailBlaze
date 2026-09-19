namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.Admin;

/// <summary>
/// Puts the configured administrator into the database, or confirms they are already there.
/// </summary>
/// <remarks>
/// <para>
/// The operation is an upsert, and the *idempotence* is the part worth naming as a contract:
/// the caller is a startup path that runs on every boot of every replica, so a second call
/// finding the work done must be as unremarkable as a first call doing it. The returned
/// <see cref="AdminSeedOutcome"/> says which of the two happened; neither is a failure.
/// </para>
/// <para>
/// It takes no argument because there is nothing to choose. Which administrator is configured
/// is a deployment fact, not a request, and a parameter naming one would be a way for a caller
/// to appoint somebody — the exact screen this feature exists not to have.
/// </para>
/// </remarks>
public interface IAdminSeedingService
{
    /// <summary>
    /// Applies the configured administrator to the database.
    /// </summary>
    /// <returns>Which of the three outcomes the database was in.</returns>
    /// <exception cref="Exception">
    /// The database could not be reached, or refused the write. Deliberately untyped here: this
    /// layer has no reference to a database provider and should not acquire one to name a
    /// failure it does not handle. What a failure means for the process is the caller's
    /// question — see <c>AdminSeedingHostedService</c>.
    /// </exception>
    Task<AdminSeedOutcome> SeedAdminAsync();
}
