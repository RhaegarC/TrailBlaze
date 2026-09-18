namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The model and the migration set, checked without a database — deliberately, not for want
/// of one. These are metadata questions, so they run on every machine including one with no
/// container. See <see cref="OfflineContext"/>.
/// </summary>
public sealed class PersistenceModelTests
{
    /// <summary>
    /// The provider swap is the whole point of part of feature 01: the scaffold shipped
    /// against PostgreSQL. Asserting the provider name is what stops the swap from silently
    /// reverting the next time someone edits the registration.
    /// </summary>
    [Fact]
    public void The_context_is_configured_for_sql_server()
    {
        using var offline = new OfflineContext();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", offline.Context.Database.ProviderName);
    }

    /// <summary>
    /// The migrations and the model must agree, or the schema a deployment applies is not
    /// the schema the code expects. This compares the model to the snapshot and never
    /// connects; that the set also <em>applies</em> is
    /// <c>DatabaseSchemaTests</c>'s subject, which does need a server.
    /// </summary>
    [Fact]
    public void The_model_matches_the_migration_snapshot()
    {
        using var offline = new OfflineContext();

        Assert.False(
            offline.Context.Database.HasPendingModelChanges(),
            "The model and the migration snapshot disagree. Regenerate with "
            + "`dotnet ef migrations add <name>` and commit the result.");
    }

    /// <summary>
    /// The audit snapshots were PostgreSQL's <c>jsonb</c> before the provider swap, and a
    /// column type is exactly the kind of thing that survives a swap unnoticed until a write
    /// fails. Both JSON columns must be string-typed and unbounded: <c>nvarchar(max)</c>
    /// holds the JSON, and a bounded type would truncate it silently.
    /// </summary>
    [Fact]
    public void The_audit_snapshots_are_unbounded_strings()
    {
        using var offline = new OfflineContext();

        var entity = offline.Context.Model
            .FindEntityType(typeof(TrailBlaze.Model.DatabaseEntity.AuditLog))!;

        string oldValues = entity.FindProperty(nameof(TrailBlaze.Model.DatabaseEntity.AuditLog.OldValues))!
            .GetColumnType();
        string newValues = entity.FindProperty(nameof(TrailBlaze.Model.DatabaseEntity.AuditLog.NewValues))!
            .GetColumnType();

        Assert.Equal("nvarchar(max)", oldValues);
        Assert.Equal("nvarchar(max)", newValues);
    }
}
