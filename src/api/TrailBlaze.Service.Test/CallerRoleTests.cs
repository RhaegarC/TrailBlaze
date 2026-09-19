namespace TrailBlaze.Service.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// What a role check reads, and what it does when there is nothing to read.
/// </summary>
/// <remarks>
/// This is the input every authorization decision in feature 09 will be made from, so the
/// interesting assertions are the ones about failure. The successful case is one line — the row
/// holds a value and the caller gets it — and the cases that decide whether the app is safe are
/// the ones where the answer is not a role: a caller with no row, a caller with no identity, a
/// row that has been soft-deleted. Each of those must come back as <c>null</c> rather than as a
/// default, because <c>null</c> is what a caller cannot mistake for permission.
/// </remarks>
[Trait("Category", "Container")]
public sealed class CallerRoleTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    /// <summary>
    /// The value the role check returns is the one stored on the caller's row.
    /// </summary>
    /// <remarks>
    /// Changed between the two reads through <c>Role</c>, not through the check, so this also
    /// shows the answer is read per call rather than captured: a cached role would keep
    /// answering <c>User</c> after the row changed.
    /// </remarks>
    [SkippableFact]
    public async Task The_role_is_read_from_the_callers_own_row()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string objectId = Guid.NewGuid().ToString();

        await WriteUserAsync(objectId, Constant.UserRole.User);
        Assert.Equal(Constant.UserRole.User, await RoleOfCallerAsync(objectId));

        await SetRoleAsync(objectId, Constant.UserRole.Admin);
        Assert.Equal(Constant.UserRole.Admin, await RoleOfCallerAsync(objectId));
    }

    /// <summary>
    /// A caller whose row does not exist has no role — and asking does not create one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The row is deliberately not provisioned here, and that differs from the profile path on
    /// purpose. <c>IUserService.GetOrCreateAsync</c> writes a row because a profile request is
    /// a request to have one; an authorization check is a read, and a read that writes is a
    /// surprise — it would make every 401 the app returns a user-creating operation.
    /// </para>
    /// <para>
    /// The second assertion is the one worth having: "no role" is satisfied just as well by a
    /// check that creates the row and then reads it. Counting the rows proves nothing was
    /// written.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task A_caller_with_no_row_has_no_role_and_gets_no_row()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string objectId = Guid.NewGuid().ToString();

        Assert.Null(await RoleOfCallerAsync(objectId));
        Assert.Equal(0, await CountUsersAsync(objectId));
    }

    /// <summary>
    /// A request that carries no identity has no role.
    /// </summary>
    /// <remarks>
    /// Anonymous and unauthenticated are the same answer here, and the same answer as a missing
    /// row. There is deliberately no fallback to anything — not to a default role, not to any
    /// privileged one — because a value returned when the caller could not be established is a
    /// value returned to whoever asked.
    /// </remarks>
    [SkippableFact]
    public async Task A_caller_with_no_identity_has_no_role()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();

        var anonymous = new CallerRoleService(
            scope.ServiceProvider.GetRequiredService<IDbRepository>(),
            FakeUserContext.AnonymousRequest());

        Assert.Null(await anonymous.GetRoleAsync());

        var noRequest = new CallerRoleService(
            scope.ServiceProvider.GetRequiredService<IDbRepository>(),
            FakeUserContext.NoRequest());

        Assert.Null(await noRequest.GetRoleAsync());
    }

    /// <summary>
    /// A soft-deleted row confers nothing, even if it says <c>Admin</c>.
    /// </summary>
    /// <remarks>
    /// The soft-delete filter is applied by convention across every <see cref="EntityBase"/>
    /// type, so this comes free — which is precisely why it is worth asserting rather than
    /// assuming. The row is still physically present and still reads <c>Admin</c> to anyone who
    /// queries around the filter, and the correctness of this check rests on the filter being
    /// the default rather than on anything this service does.
    /// </remarks>
    [SkippableFact]
    public async Task A_deleted_users_row_confers_no_role()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string objectId = Guid.NewGuid().ToString();

        await WriteUserAsync(objectId, Constant.UserRole.Admin);

        using (IServiceScope scope = fixture.CreateScope())
        {
            TrailBlazeContext context = ContextOf(scope);
            User user = await context.Users.SingleAsync(row => row.Id == objectId);

            user.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        Assert.Null(await RoleOfCallerAsync(objectId));
    }

    /// <summary>
    /// The role <paramref name="objectId"/> resolves to, through a caller of its own.
    /// </summary>
    private async Task<string?> RoleOfCallerAsync(string objectId)
    {
        using IServiceScope scope = fixture.CreateScope();

        var caller = new CallerRoleService(
            scope.ServiceProvider.GetRequiredService<IDbRepository>(),
            FakeUserContext.Authenticated(objectId));

        return await caller.GetRoleAsync();
    }

    private async Task WriteUserAsync(string objectId, string role)
    {
        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = ContextOf(scope);

        context.Users.Add(new User
        {
            Id = objectId,
            DisplayName = "Someone",
            Role = role,
        });

        await context.SaveChangesAsync();
    }

    private async Task SetRoleAsync(string objectId, string role)
    {
        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = ContextOf(scope);

        User user = await context.Users.SingleAsync(row => row.Id == objectId);

        user.Role = role;
        await context.SaveChangesAsync();
    }

    private async Task<int> CountUsersAsync(string objectId)
    {
        using IServiceScope scope = fixture.CreateScope();

        return await ContextOf(scope).Users.CountAsync(user => user.Id == objectId);
    }

    private static TrailBlazeContext ContextOf(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
}
