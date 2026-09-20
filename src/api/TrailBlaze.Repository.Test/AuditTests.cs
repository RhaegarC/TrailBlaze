namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// Audit stamping, against a real SQL Server. These assert what a caller can observe after a
/// save: the history row, read back out of the database, and the audit columns.
/// </summary>
/// <remarks>
/// Every assertion here is a round trip. The history is read through a fresh scope rather than
/// the change tracker that wrote it, so "recorded" means the database holds it — which is a
/// stronger claim than the previous offline tier could make, and the reason this file now
/// needs a container.
/// </remarks>
[Trait("Category", "Container")]
public sealed class AuditTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    [SkippableFact]
    public async Task An_insert_is_recorded_with_its_key_and_table()
    {
        using var harness = fixture.CreateHarness();
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);

        harness.Save();

        AuditLog log = await harness.SingleAuditEntryAsync(user.Id);
        Assert.Equal("Users", log.TableName);
        Assert.Equal(user.Id, log.EntityId);
        Assert.Equal("Added", log.Action);
    }

    /// <summary>
    /// The key is on the entity before the save, which is why an insert can be audited with
    /// a real id instead of a placeholder — the property STANDARD §3 exists to protect.
    /// </summary>
    [SkippableFact]
    public async Task A_new_entity_has_its_key_before_the_save()
    {
        using var harness = fixture.CreateHarness();
        var user = new User();

        Assert.False(string.IsNullOrWhiteSpace(user.Id));
        Assert.True(Guid.TryParse(user.Id, out _));

        harness.Context.Users.Add(user);
        harness.Save();

        Assert.Equal(user.Id, (await harness.SingleAuditEntryAsync(user.Id)).EntityId);
    }

    [SkippableFact]
    public async Task The_async_save_path_is_audited_too()
    {
        using var harness = fixture.CreateHarness();
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);

        await harness.SaveAsync();

        AuditLog log = await harness.SingleAuditEntryAsync(user.Id);
        Assert.Equal("Users", log.TableName);
        Assert.Equal(user.Id, log.EntityId);
    }

    [SkippableFact]
    public async Task An_insert_stamps_the_audit_columns()
    {
        using var harness = fixture.CreateHarness(FakeUserContext.Authenticated("oid-ada"));
        var user = new User();
        harness.Context.Users.Add(user);

        harness.Save();

        Assert.NotEqual(default, user.CreatedOn);
        Assert.Equal("oid-ada", user.CreatedBy);
        Assert.False(user.IsDeleted);

        // Stamped in memory is not the same as stamped in the row, so the database is asked.
        User stored = (await harness.FindUserAsync(user.Id))!;
        Assert.Equal("oid-ada", stored.CreatedBy);
        Assert.Equal(user.CreatedOn, stored.CreatedOn);
    }

    [SkippableFact]
    public async Task Work_with_no_request_at_all_is_attributed_to_the_system()
    {
        using var harness = fixture.CreateHarness(FakeUserContext.NoRequest());
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);

        harness.Save();

        Assert.Equal("system", (await harness.SingleAuditEntryAsync(user.Id)).Actor);
    }

    [SkippableFact]
    public async Task An_unauthenticated_request_is_recorded_as_anonymous()
    {
        using var harness = fixture.CreateHarness(FakeUserContext.AnonymousRequest());
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);

        harness.Save();

        Assert.Equal("anonymous", (await harness.SingleAuditEntryAsync(user.Id)).Actor);
    }

    [SkippableFact]
    public async Task An_authenticated_request_is_recorded_against_the_object_id()
    {
        using var harness = fixture.CreateHarness(FakeUserContext.Authenticated("oid-ada"));
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);

        harness.Save();

        Assert.Equal("oid-ada", (await harness.SingleAuditEntryAsync(user.Id)).Actor);
    }

    /// <summary>
    /// A missing token and a background job are different problems, so they must not collapse
    /// into one value. This is the pairing that would catch someone "simplifying" the
    /// fallback to a single "unknown".
    /// </summary>
    [SkippableFact]
    public async Task An_anonymous_request_and_a_missing_request_are_recorded_differently()
    {
        var anonymousUser = new User();

        using (var anonymous = fixture.CreateHarness(FakeUserContext.AnonymousRequest()))
        {
            anonymous.Context.Users.Add(anonymousUser);
            await anonymous.SaveAsync();

            Assert.Equal("anonymous", (await anonymous.SingleAuditEntryAsync(anonymousUser.Id)).Actor);
        }

        var noRequestUser = new User();

        using (var noRequest = fixture.CreateHarness(FakeUserContext.NoRequest()))
        {
            noRequest.Context.Users.Add(noRequestUser);
            await noRequest.SaveAsync();

            Assert.Equal("system", (await noRequest.SingleAuditEntryAsync(noRequestUser.Id)).Actor);
        }
    }

    [SkippableFact]
    public async Task The_request_context_travels_with_the_audit_row()
    {
        using var harness = fixture.CreateHarness(FakeUserContext.Authenticated("oid-ada"));
        harness.User.ActorName = "Ada Lovelace";
        harness.User.IpAddress = "203.0.113.7";
        harness.User.UserAgent = "TrailBlaze.Tests/1.0";
        harness.User.CorrelationId = "correlation-1";

        var user = new User();
        harness.Context.Users.Add(user);

        harness.Save();

        AuditLog log = await harness.SingleAuditEntryAsync(user.Id);
        Assert.Equal("Ada Lovelace", log.ActorName);
        Assert.Equal("203.0.113.7", log.IpAddress);
        Assert.Equal("TrailBlaze.Tests/1.0", log.UserAgent);
        Assert.Equal("correlation-1", log.CorrelationId);
    }

    /// <summary>
    /// The history row and the change it describes reach the database together, rather than
    /// leaving a change with no history.
    /// </summary>
    /// <remarks>
    /// This replaces a test that asserted the two were "staged on the same context", which
    /// was a statement about EF's change tracker and not about the database at all — it could
    /// not distinguish one transaction from two. Both rows being readable afterwards is a
    /// weaker-looking claim that is actually about the thing the old one only implied.
    /// <see cref="A_failed_save_leaves_neither_the_change_nor_its_history"/> is the half that
    /// makes it a real claim about the transaction.
    /// </remarks>
    [SkippableFact]
    public async Task The_change_and_its_history_are_both_persisted()
    {
        using var harness = fixture.CreateHarness();
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);

        harness.Save();

        Assert.NotNull(await harness.FindUserAsync(user.Id));
        Assert.Equal("Added", (await harness.SingleAuditEntryAsync(user.Id)).Action);
    }

    /// <summary>
    /// A save that fails leaves neither the change nor its history. This is the claim no
    /// offline test could make, and the reason the interceptor adds its rows to the caller's
    /// own context instead of saving them separately.
    /// </summary>
    /// <remarks>
    /// The failure is a primary-key collision: a second insert naming an id that already
    /// exists. EF sends both that insert and the audit row in one transaction, so the
    /// violation takes the history row back with it. Had the interceptor committed its rows
    /// on its own connection, the history would survive a change that did not — an audit
    /// trail describing something that never happened, which is worse than no trail at all.
    /// </remarks>
    [SkippableFact]
    public async Task A_failed_save_leaves_neither_the_change_nor_its_history()
    {
        string id = Guid.NewGuid().ToString();

        using (var seed = fixture.CreateHarness())
        {
            seed.Context.Users.Add(new User { Id = id, DisplayName = "Ada" });
            await seed.SaveAsync();
        }

        using (var collision = fixture.CreateHarness())
        {
            collision.Context.Users.Add(new User { Id = id, DisplayName = "Grace" });

            await Assert.ThrowsAsync<DbUpdateException>(() => collision.SaveAsync());
        }

        using var reader = fixture.CreateHarness();

        // Still one row, and still the first one: the second insert's audit row went back with
        // its failed change rather than being written ahead of it.
        User stored = (await reader.FindUserAsync(id))!;
        Assert.Equal("Ada", stored.DisplayName);
        Assert.Single(await reader.AuditEntriesAsync(id));
    }

    /// <summary>
    /// The update branch of the interceptor: it stamps the modification and records the row
    /// as "Modified".
    /// </summary>
    /// <remarks>
    /// This was the test the offline tier could not honestly write. It used to attach a
    /// <c>User</c> as <c>Modified</c> with no row behind it, which only worked because the
    /// save failed before reaching a server — its own remarks said so. Against a real engine
    /// that UPDATE matches zero rows and EF raises <c>DbUpdateConcurrencyException</c>, which
    /// is what that arrangement was hiding. It is now a genuine insert, then a change to a row
    /// that exists, and the history holds both events.
    /// </remarks>
    [SkippableFact]
    public async Task A_modified_entity_is_recorded_as_modified_and_stamped()
    {
        using var harness = fixture.CreateHarness(FakeUserContext.Authenticated("oid-ada"));
        var user = new User { DisplayName = "Ada" };
        harness.Context.Users.Add(user);
        await harness.SaveAsync();

        // No Attach, no Update, no explicit state: the row is loaded by the insert that just
        // ran, so the change tracker notices the edit the way it does in the application.
        user.DisplayName = "Ada Lovelace";
        await harness.SaveAsync();

        AuditLog modified = await harness.AuditEntryAsync(user.Id, "Modified");

        Assert.Equal(user.Id, modified.EntityId);
        Assert.Equal("Users", modified.TableName);
        Assert.Equal("oid-ada", user.LastModifiedBy);
        Assert.NotEqual(default, user.LastModifiedOn);

        // Two events, and the insert is still there: the update did not replace its history.
        Assert.Equal(2, (await harness.AuditEntriesAsync(user.Id)).Count);
        Assert.Equal("Ada Lovelace", (await harness.FindUserAsync(user.Id))!.DisplayName);

        // The snapshot is the real before-and-after, not the same value written twice: the
        // old state holds what the row had, the new state what it now has.
        Assert.NotNull(modified.OldValues);
        Assert.Contains("\"Ada\"", modified.OldValues);
        Assert.NotNull(modified.NewValues);
        Assert.Contains("Ada Lovelace", modified.NewValues);
    }
}
