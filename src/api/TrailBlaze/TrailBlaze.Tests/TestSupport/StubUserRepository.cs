using System.Linq.Expressions;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Tests.TestSupport
{
    /// <summary>
    /// Stands in for the EF-backed <c>UserRepository</c> so <c>UserService</c>'s read-then-insert
    /// decisions can be tested with no database. The service looks a user up by primary key, so
    /// the predicate is compiled and applied to the stored row rather than treated as "any
    /// caller matches" — a double that ignores the query would pass while the service looked the
    /// wrong person up.
    /// </summary>
    internal sealed class StubUserRepository : IUserRepository
    {
        /// <summary>The row the lookup should find. Null models a caller never seen before.</summary>
        public User? Stored { get; init; }

        /// <summary>Every entity handed to <see cref="CreateAsync{T}(T)"/>, in order.</summary>
        public List<User> Created { get; } = new();

        public Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            T? found = Stored as T;
            return Task.FromResult(found is not null && predicate.Compile()(found) ? found : null);
        }

        public Task<int> CreateAsync<T>(T item)
        {
            User created = item as User
                ?? throw new NotSupportedException("This double only handles User.");

            Created.Add(created);
            return Task.FromResult(1);
        }

        // The service never reaches these. They throw rather than returning an empty result,
        // so a test that accidentally exercises one fails loudly instead of passing on a
        // plausible-looking default.
        public Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class =>
            throw new NotSupportedException();

        public Task<int> CreateAsync<T>(List<T> items) => throw new NotSupportedException();

        public Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase =>
            throw new NotSupportedException();

        public Task<int> UpdateAsync<T>(T item) where T : EntityBase => throw new NotSupportedException();

        public Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase =>
            throw new NotSupportedException();
    }
}
