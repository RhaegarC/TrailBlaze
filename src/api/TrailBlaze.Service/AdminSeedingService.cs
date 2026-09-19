namespace TrailBlaze.Service;

using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Admin;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// Puts the configured administrator in place: creates the row if it is missing, promotes it if
/// it is not already an administrator, and does nothing at all if it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotence comes from reading before writing.</b> The decision is made entirely from the
/// row that is there, so a second run over a database that has already been seeded takes the
/// third branch and issues no write — no update, no save, and therefore no audit entry. Nothing
/// here needs to remember that it has run: whether it should run is a question the database
/// answers, and one that survives the process, which is what a restart and a second replica
/// both require.
/// </para>
/// <para>
/// <b>The race, stated rather than claimed away.</b> The read and the write are two statements
/// and not one transaction, so two replicas starting together against an unseeded database can
/// both find nothing and both insert. The primary key admits one: the loser fails its insert
/// loudly, and the state is correct anyway, because what the winner inserted <em>is</em> the
/// administrator. That is the same trade <c>UserService.GetOrCreateAsync</c> makes for
/// provisioning, and for the same reason — a duplicate-key collision that leaves the right row
/// behind is cheaper than serializing every startup behind a lock. It is visible in the log on
/// the replica that lost rather than silent, which is the part that matters: a failure nobody
/// reports is indistinguishable from one that did not happen.
/// </para>
/// <para>
/// <b>An existing row keeps everything except its role.</b> Only <c>Role</c> is assigned on the
/// promote path. The person this happens to was already using the app — their display name,
/// their avatar, their theme and their bio are theirs — and a deploy that re-saved the row from
/// configuration would take all of it away as a side effect of granting a permission.
/// </para>
/// <para>
/// <b>A soft-deleted row for the configured id is a state this cannot repair.</b> The query
/// filter hides it, so seeding sees nothing, inserts, and collides on the key — every start,
/// with the row still deleted. Nothing in this application soft-deletes a user (there is no
/// role-management API, and feature 09 adds no account deletion), so reaching that state takes a
/// deliberate SQL edit, and undoing it takes the same. Recorded here because the failure it
/// produces is a startup log line rather than an obvious one.
/// </para>
/// </remarks>
public sealed class AdminSeedingService(IDbRepository dbRepository, AdminSeed adminSeed)
    : IAdminSeedingService
{
    /// <inheritdoc/>
    public async Task<AdminSeedOutcome> SeedAdminAsync()
    {
        User? admin = await dbRepository.GetAsync<User>(user => user.Id == adminSeed.EntraObjectId);

        if (admin is null)
        {
            await dbRepository.CreateAsync(new User
            {
                Id = adminSeed.EntraObjectId,
                DisplayName = adminSeed.DisplayName,
                Role = Constant.UserRole.Admin,
            });

            return AdminSeedOutcome.Inserted;
        }

        if (admin.Role == Constant.UserRole.Admin)
        {
            return AdminSeedOutcome.AlreadyAdmin;
        }

        admin.Role = Constant.UserRole.Admin;
        await dbRepository.UpdateAsync(admin);

        return AdminSeedOutcome.Promoted;
    }
}
