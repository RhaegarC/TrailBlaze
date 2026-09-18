namespace TrailBlaze.Repository.Test;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// What the engine does when two writes claim the same identity.
/// </summary>
/// <remarks>
/// <para>
/// <c>UserService.GetOrCreateAsync</c> reads a row and, finding none, inserts one keyed on the
/// Entra object id. Its own comment says the read and the write are not one transaction, so
/// two requests for the same unseen caller can both find nothing and both insert — and that
/// "the primary key still admits only one row, so the loser fails its insert rather than
/// corrupting anything".
/// </para>
/// <para>
/// That is a claim about the schema, not a comment about it, and nothing tested it. A schema
/// change that dropped the uniqueness of <c>Users.Id</c> would leave the sentence in place
/// while making it false, and the failure would be two rows for one caller — a silent
/// duplicate that no later read could tell from the real one. These two tests make the claim
/// executable.
/// </para>
/// <para>
/// <b>The race itself is not tested here and cannot be.</b> Reproducing it needs two
/// simultaneous requests, which is load evidence rather than a correctness test, and it is
/// feature 11's. What is tested is what the comment rests on: that the loser's failure is a
/// duplicate-key error a catch could recognise, and that only one row survives it.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class DuplicateKeyTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    /// <summary>
    /// The second insert for an identity already taken fails on the key rather than
    /// succeeding.
    /// </summary>
    /// <remarks>
    /// The error number is the point rather than the exception type. A retry-on-duplicate is
    /// the fix the service's own comment names, and a retry has to recognise the failure to
    /// act on it — so what a caller can actually key on is asserted here, from the engine,
    /// instead of guessed at from documentation.
    /// </remarks>
    [SkippableFact]
    public async Task A_second_row_with_the_same_id_fails_on_the_key()
    {
        string id = await SeedAsync("Ada");

        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        DbUpdateException thrown = await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.CreateAsync(new User { Id = id, DisplayName = "Grace" }));

        SqlException? failure = SqlFailures.Find(thrown);

        Assert.True(
            failure is not null,
            $"Inserting a second row with the same Id should have failed on the primary key. "
            + $"Got {thrown.GetType().Name}: {thrown.Message}");

        // Measured against this engine: 2627, because `Users.Id` is declared as a PRIMARY KEY.
        // 2601 is the UNIQUE index form and is accepted too -- a schema change moving the
        // uniqueness to an index would swap the number without changing what went wrong, and
        // this assertion is about the refusal rather than about which object enforced it.
        Assert.Contains(failure.Number, SqlFailures.DuplicateKey);
    }

    /// <summary>
    /// The collision leaves exactly one row, and it is the one that was written first.
    /// </summary>
    /// <remarks>
    /// This is the half that matters to a reader. "The loser fails" is only reassuring if the
    /// winner's row is still there and still readable — a schema that admitted both would
    /// return two users for one caller, and a schema that lost both would leave the caller
    /// unable to sign in at all. The surviving row is checked by content rather than by count
    /// alone, because a count of one passes just as well if the second write overwrote the
    /// first, which is a different bug with the same shape.
    /// </remarks>
    [SkippableFact]
    public async Task The_collision_leaves_the_first_row_alone()
    {
        string id = await SeedAsync("Ada");

        using (IServiceScope losing = fixture.CreateScope())
        {
            var repository = new DatabaseRepository(
                losing.ServiceProvider.GetRequiredService<TrailBlazeContext>());

            Exception? refused = await Record.ExceptionAsync(
                () => repository.CreateAsync(new User { Id = id, DisplayName = "Grace" }));

            // Asserted, not just swallowed. A single surviving row would also be the result
            // if the second write had failed for some unrelated reason, and the test would
            // then be green while proving nothing about the key.
            Assert.IsType<DbUpdateException>(refused);
        }

        // A fresh scope, so the row comes from the database rather than from the change
        // tracker of a context that has just watched the write fail.
        using IServiceScope reading = fixture.CreateScope();
        TrailBlazeContext context =
            reading.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        List<User> rows = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .ToListAsync();

        User survivor = Assert.Single(rows);
        Assert.Equal("Ada", survivor.DisplayName);
    }

    /// <summary>
    /// Writes one row under a fresh identity and returns that identity.
    /// </summary>
    /// <remarks>
    /// A GUID per test, because the fixture's database is shared with the rest of the class —
    /// and with every other class in the collection. A literal id would collide across tests
    /// in both directions: this test's seed would fail, or another test's.
    /// </remarks>
    private async Task<string> SeedAsync(string displayName)
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string id = Guid.NewGuid().ToString();

        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        await repository.CreateAsync(new User { Id = id, DisplayName = displayName });

        return id;
    }
}
