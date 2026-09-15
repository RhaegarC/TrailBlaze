using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Interface.Service
{
    public interface IUserService
    {
        /// <summary>
        /// The caller's own row, inserted on first sight. Resolved from the request in flight
        /// rather than from an argument, so a caller cannot ask for somebody else's record.
        /// </summary>
        /// <returns>The caller's row, or null when the request carries no Entra Object ID to
        /// key one on.</returns>
        Task<User?> GetOrCreateAsync();
    }
}
