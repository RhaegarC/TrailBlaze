namespace TrailBlaze.Interface.Repository;

using System.Linq.Expressions;
using TrailBlaze.Model.DatabaseEntity;

public interface IDbRepository
{
    /// <summary>
    /// Get a single item.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="predicate">Condition.</param>
    /// <returns>Item entity.</returns>
    Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

    /// <summary>
    /// Get item list.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="predicate">Condition.</param>
    /// <returns>Result</returns>
    Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class;

    /// <summary>
    /// Get one page of an ordered, filtered set, and the size of that set.
    /// </summary>
    /// <remarks>
    /// The store does the ordering, the filtering and the paging, which is what keeps a row the
    /// caller may not see from consuming a page slot, and what keeps the reported total the size
    /// of the filtered set rather than of the table.
    /// </remarks>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="predicate">Condition.</param>
    /// <param name="orderBy">The ordering, applied before the page is taken.</param>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Rows to take.</param>
    /// <returns>The page's items, and the count of every row the predicate matches.</returns>
    Task<(List<T> Items, int Total)> GetPageAsync<T>(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        int skip,
        int take) where T : class;

    /// <summary>
    /// Create new item.
    /// </summary>
    /// <param name="item">Item.</param>
    /// <returns>Count of item created.</returns>
    Task<int> CreateAsync<T>(T item);

    /// <summary>
    /// Creates an item only while a counted set has room for it, as one atomic step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two statements, not one. A caller that counted, decided, and then inserted would be reading
    /// a number another request can change between the two — so a per-activity cap checked that way
    /// admits one row too many exactly when two uploads arrive together, which is the case the cap
    /// exists for.
    /// </para>
    /// <para>
    /// The transaction is what closes that window, and it is why this is a repository operation
    /// rather than something a service assembles: the context lives here. Isolation is serializable
    /// rather than the default, because a range lock is what stops the second reader seeing a count
    /// the first has not committed yet.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="item">The item to insert.</param>
    /// <param name="countOf">Which existing rows count towards the cap.</param>
    /// <param name="cap">The most rows the set may hold. The insert happens only below it.</param>
    /// <returns>True when the item was inserted, false when the cap was already reached and
    /// nothing was written.</returns>
    Task<bool> CreateIfUnderAsync<T>(T item, Expression<Func<T, bool>> countOf, int cap) where T : class;

    /// <summary>
    /// Create new items.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="items">Items.</param>
    /// <returns>Count of item created.</returns>
    Task<int> CreateAsync<T>(List<T> items);

    /// <summary>
    /// Soft delete an item from db.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="id">Ids of item to be deleted.</param>
    /// <returns>Result.</returns>
    Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase;

    /// <summary>
    /// Update one single item.
    /// </summary>
    /// <param name="item">Item to update.</param>
    /// <typeparam name="T">Item type.</typeparam>
    /// <returns>Result.</returns>
    Task<int> UpdateAsync<T>(T item) where T : EntityBase;

    /// <summary>
    /// Update items.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="items">Items.</param>
    /// <returns>Update result.</returns>
    Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase;
}
