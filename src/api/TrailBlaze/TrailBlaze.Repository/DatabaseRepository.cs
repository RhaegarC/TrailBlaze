using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Repository
{
    public abstract class DatabaseRepository(TrailBlazeContext context) : IDbRepository
    {
        private readonly TrailBlazeContext _context = context;

        /// <inheritdoc/>
        public async Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var item = await _context.Set<T>().FirstOrDefaultAsync(predicate);
            return item;
        }

        /// <inheritdoc/>
        public async Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var items = await _context.Set<T>().Where(predicate).ToListAsync();
            return items;
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync<T>(T item)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(item));
            await _context.AddAsync(item);
            int count = await _context.SaveChangesAsync();
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
                await _context.AddAsync(item);
            }

            int count = await _context.SaveChangesAsync();
            return count;
        }

        /// <inheritdoc/>
        public async Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase
        {
            foreach (var id in ids)
            {
                T? item = await _context.FindAsync<T>(id);
                if (item != null)
                {
                    item.IsDeleted = true;
                    item.LastModifiedOn = DateTime.UtcNow;
                    item.LastModifiedBy = "sys";
                    _context.Update(item);
                }
            }

            int count = await _context.SaveChangesAsync();
            return count;
        }

        /// <inheritdoc/>
        public async Task<int> UpdateAsync<T>(T item) where T : EntityBase
        {
            _context.Update(item);
            int count = await _context.SaveChangesAsync();
            return count;
        }

        /// <inheritdoc/>
        public async Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase
        {
            foreach (T item in items)
            {
                _context.Update(item);
            }

            int count = await _context.SaveChangesAsync();
            return count;
        }
    }
}
