using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository;
using TrailBlaze.Tests.TestSupport;

namespace TrailBlaze.Tests
{
    /// <summary>
    /// IM-12 settled the model on application-assigned string keys. IM-14 then depended on it:
    /// reading the key for an insert only works if the key exists before the save.
    /// </summary>
    public class KeyStrategyTests
    {
        [Fact]
        public void A_new_entity_already_has_a_key()
        {
            var user = new User();

            Assert.False(string.IsNullOrWhiteSpace(user.Id));
        }

        [Fact]
        public void Two_new_entities_do_not_share_a_key()
        {
            Assert.NotEqual(new User().Id, new User().Id);
        }

        /// <summary>
        /// The load-bearing half of the strategy: the key is the application's to assign, so
        /// EF must not be waiting on the database to produce one. If this ever drifted back to
        /// <c>ValueGenerated.OnAdd</c>, the key would still be populated in memory while EF
        /// silently ignored it in favour of the store's value.
        /// </summary>
        [Theory]
        [InlineData(typeof(User))]
        [InlineData(typeof(AuditLog))]
        public void Keys_are_not_generated_by_the_database(Type entityType)
        {
            using var context = NewContext();

            var key = context.Model.FindEntityType(entityType)?.FindPrimaryKey();

            Assert.NotNull(key);
            Assert.All(key.Properties, property =>
                Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
        }

        [Fact]
        public void The_key_is_a_string_column()
        {
            using var context = NewContext();

            var key = context.Model.FindEntityType(typeof(User))!.FindPrimaryKey()!;

            Assert.All(key.Properties, property =>
                Assert.Equal(typeof(string), property.ClrType));
        }

        private static TrailBlazeContext NewContext() =>
            new(new DbContextOptionsBuilder<TrailBlazeContext>()
                .UseNpgsql(AuditHarness.UnreachableConnectionString)
                .Options);
    }
}
