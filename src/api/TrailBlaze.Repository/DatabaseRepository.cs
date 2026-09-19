namespace TrailBlaze.Repository;

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// The generic CRUD every repository shares, over <see cref="TrailBlazeContext"/>.
/// </summary>
/// <remarks>
/// Concrete rather than abstract: it is the whole of the data access this app performs
/// through EF, so it is registered as <see cref="IDbRepository"/> directly rather than
/// through a per-entity subclass that would add no behaviour. A type that genuinely needs
/// entity-specific access still derives from this and keeps the base's operations available.
/// </remarks>
public class DatabaseRepository(TrailBlazeContext context) : IDbRepository
{
    /// <summary>
    /// The context every operation here runs against, exposed to derived repositories rather
    /// than kept private.
    /// </summary>
    /// <remarks>
    /// A derived type that declares its own primary-constructor parameter for the same
    /// context compiles but captures a second copy of it (CS9107), which is both a
    /// duplicate field and a claim — that the two could ever differ — that is not true.
    /// Reading the one the base already holds keeps that impossible.
    /// </remarks>
    protected TrailBlazeContext Context { get; } = context;

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        var item = await Context.Set<T>().FirstOrDefaultAsync(predicate);
        return item;
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        var items = await Context.Set<T>().Where(predicate).ToListAsync();
        return items;
    }

    /// <inheritdoc/>
    public async Task<(List<T> Items, int Total)> GetPageAsync<T>(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        int skip,
        int take) where T : class
    {
        IQueryable<T> query = Context.Set<T>().Where(predicate);

        // Counted after the filter and before the take: the total is what the caller can page through.
        int total = await query.CountAsync();
        List<T> items = await orderBy(query).Skip(skip).Take(take).ToListAsync();

        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<int> CreateAsync<T>(T item)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(item));
        await Context.AddAsync(item);
        int count = await Context.SaveChangesAsync();
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> CreateAsync<T>(List<T> items)
    {
        // A plain foreach, not List<T>.ForEach: that takes an Action<T>, so an async lambda
        // is async void -- nothing awaits it and SaveChangesAsync can run before the adds
        // finish, persisting a partial batch.
        ArgumentNullException.ThrowIfNull(items, nameof(items));

        foreach (T item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(item));
            await Context.AddAsync(item);
        }

        int count = await Context.SaveChangesAsync();
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase
    {
        foreach (var id in ids)
        {
            T? item = await Context.FindAsync<T>(id);
            if (item != null)
            {
                // Only the soft-delete flag is set here. The audit columns are stamped by
                // AuditSaveChangesInterceptor on the save below, and it overwrites whatever a
                // repository writes first -- so assigning LastModifiedOn/LastModifiedBy here
                // would be dead code that reads as though it were load-bearing (STANDARD §3).
                item.IsDeleted = true;
                Context.Update(item);
            }
        }

        int count = await Context.SaveChangesAsync();
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateAsync<T>(T item) where T : EntityBase
    {
        Context.Update(item);
        int count = await Context.SaveChangesAsync();
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase
    {
        foreach (T item in items)
        {
            Context.Update(item);
        }

        int count = await Context.SaveChangesAsync();
        return count;
    }
}
