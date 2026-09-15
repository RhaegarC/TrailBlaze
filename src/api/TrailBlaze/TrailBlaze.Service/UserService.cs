using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Service
{
    public sealed class UserService(
        IUserRepository userRepository,
        IUserContextService userContext) : IUserService
    {
        /// <inheritdoc/>
        public async Task<User?> GetOrCreateAsync()
        {
            string? entraObjectId = userContext.EntraObjectId;

            // A validated token still might not carry an object id. There is no identity to
            // key a row on, so there is nothing to look up and — more importantly — nothing
            // that may be inserted: an unidentifiable caller must not create a user.
            if (string.IsNullOrWhiteSpace(entraObjectId))
            {
                return null;
            }

            User? user = await userRepository.GetAsync<User>(existing => existing.Id == entraObjectId);

            if (user is not null)
            {
                return user;
            }

            // The object id is the row's identity, so it is assigned here rather than left to
            // the constructor's generated key.
            user = new User
            {
                Id = entraObjectId,
                DisplayName = userContext.ActorName,
            };

            await userRepository.CreateAsync(user);

            return user;
        }
    }
}
