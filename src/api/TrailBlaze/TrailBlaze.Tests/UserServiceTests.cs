using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Service;
using TrailBlaze.Tests.TestSupport;

namespace TrailBlaze.Tests
{
    /// <summary>
    /// The behaviour behind <c>GET /user/me</c>: the caller's own row comes back, and is
    /// provisioned on the first call that mentions them.
    /// </summary>
    public class UserServiceTests
    {
        private const string ObjectId = "oid-123";

        [Fact]
        public async Task A_caller_with_no_entra_id_is_not_persisted()
        {
            var repository = new StubUserRepository();
            var service = new UserService(repository, Caller(entraObjectId: null));

            User? user = await service.GetOrCreateAsync();

            Assert.Null(user);
            Assert.Empty(repository.Created);
        }

        [Fact]
        public async Task A_first_request_creates_a_user_keyed_by_the_entra_object_id()
        {
            var repository = new StubUserRepository();
            var service = new UserService(repository, Caller(ObjectId, "Ada Lovelace"));

            User? user = await service.GetOrCreateAsync();

            Assert.NotNull(user);
            Assert.Equal(ObjectId, user!.Id);
            Assert.Equal("Ada Lovelace", user.DisplayName);

            // Exactly one insert, and it is the row that was returned.
            Assert.Same(user, Assert.Single(repository.Created));
        }

        [Fact]
        public async Task A_known_caller_is_returned_without_a_second_insert()
        {
            var existing = new User { Id = ObjectId, DisplayName = "Ada Lovelace" };
            var repository = new StubUserRepository { Stored = existing };
            var service = new UserService(repository, Caller(ObjectId, "Ada Lovelace"));

            User? user = await service.GetOrCreateAsync();

            Assert.Same(existing, user);
            Assert.Empty(repository.Created);
        }

        private static FakeUserContext Caller(string? entraObjectId, string? actorName = null) =>
            new()
            {
                EntraObjectId = entraObjectId,
                ActorName = actorName,
                HasActiveRequest = true,
            };
    }
}
