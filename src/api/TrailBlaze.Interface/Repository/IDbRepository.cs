using System.Linq.Expressions;
using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Interface.Repository
{
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
        /// Create new item.
        /// </summary>
        /// <param name="item">Item.</param>
        /// <returns>Count of item created.</returns>
        Task<int> CreateAsync<T>(T item);

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
}
