namespace TrailBlaze.Service.Test
{
    using Microsoft.Extensions.DependencyInjection;
    using TrailBlaze.Interface.Infrastructure;
    using TrailBlaze.Model;
    using TrailBlaze.Service.Test.TestSupport;

    /// <summary>
    /// The storage seam at unit level: that callers can be tested against the in-memory fake,
    /// offline, and that the fake records enough to assert container routing later.
    /// </summary>
    public sealed class StorageSeamTests
    {
        /// <summary>
        /// Stands in for the service-layer types features 02, 06 and 08 will add. It exists so
        /// the wiring is exercised now rather than being assumed until the first real consumer
        /// arrives.
        /// </summary>
        private sealed class AvatarWriter(IStorageService storage)
        {
            public Task<string> WriteAsync(string name, byte[] bytes, CancellationToken ct = default) =>
                storage.UploadAsync(
                    Constant.StorageContainer.Avatars,
                    name,
                    new MemoryStream(bytes),
                    "image/png",
                    ct);
        }

        /// <summary>
        /// The load-bearing assertion of this tier: a service that depends on storage resolves
        /// against the fake and completes. If it were bound to the Azure implementation this
        /// would need credentials and a network, and the RED → GREEN loop would stop being
        /// offline (PRD Decision #6).
        /// </summary>
        [Fact]
        public async Task A_service_depending_on_storage_resolves_against_the_fake_and_needs_no_network()
        {
            var fake = new FakeStorageService();
            var services = new ServiceCollection();
            services.AddSingleton<IStorageService>(fake);
            services.AddSingleton<AvatarWriter>();

            await using ServiceProvider provider = services.BuildServiceProvider();
            var writer = provider.GetRequiredService<AvatarWriter>();

            string path = await writer.WriteAsync("ada.png", [1, 2, 3]);

            Assert.Equal("ada.png", path);

            // The recorded call, not the registration: the test itself registered the fake, so
            // asserting the container is what proves the caller routed rather than merely ran.
            Assert.Equal(Constant.StorageContainer.Avatars, fake.SingleCall().Container);
        }

        [Fact]
        public async Task An_upload_returns_a_path_and_the_fake_holds_the_bytes()
        {
            var fake = new FakeStorageService();

            string path = await fake.UploadAsync(
                Constant.StorageContainer.Covers,
                "hike/cover.jpg",
                new MemoryStream([9, 8, 7]),
                "image/jpeg");

            Assert.Equal("hike/cover.jpg", path);
            Assert.Equal<byte[]>([9, 8, 7], fake.Bytes(Constant.StorageContainer.Covers, path)!);
        }

        [Fact]
        public async Task A_delete_removes_the_bytes()
        {
            var fake = new FakeStorageService();
            await fake.UploadAsync(
                Constant.StorageContainer.Media,
                "hike/clip.mp4",
                new MemoryStream([1]),
                "video/mp4");

            await fake.DeleteAsync(Constant.StorageContainer.Media, "hike/clip.mp4");

            Assert.False(fake.Exists(Constant.StorageContainer.Media, "hike/clip.mp4"));
        }

        /// <summary>Deleting something already gone is a cleanup, not a failure — the contract
        /// the real implementation also honours with <c>DeleteIfExists</c>.</summary>
        [Fact]
        public async Task A_delete_of_something_absent_is_not_an_error()
        {
            var fake = new FakeStorageService();

            await fake.DeleteAsync(Constant.StorageContainer.Media, "never-existed.mp4");

            Assert.False(fake.Exists(Constant.StorageContainer.Media, "never-existed.mp4"));
        }

        /// <summary>
        /// The container is a parameter, not a property, and the fake records it. This is what
        /// feature 08 relies on to prove a visibility change moved the object between the public
        /// and private containers rather than copying it within one.
        /// </summary>
        [Fact]
        public async Task A_move_records_both_containers_and_relocates_the_bytes()
        {
            var fake = new FakeStorageService();
            await fake.UploadAsync(
                Constant.StorageContainer.Media,
                "hike/clip.mp4",
                new MemoryStream([5, 5]),
                "video/mp4");

            await fake.MoveAsync(
                Constant.StorageContainer.Media, "hike/clip.mp4",
                Constant.StorageContainer.Covers, "hike/cover.mp4");

            StorageCall call = fake.SingleCallOf(StorageOperation.Move);
            Assert.Equal(Constant.StorageContainer.Media, call.Container);
            Assert.Equal(Constant.StorageContainer.Covers, call.DestinationContainer);
            Assert.False(fake.Exists(Constant.StorageContainer.Media, "hike/clip.mp4"));
            Assert.Equal<byte[]>([5, 5], fake.Bytes(Constant.StorageContainer.Covers, "hike/cover.mp4")!);
        }

        /// <summary>
        /// A move is visible from either end. Feature 08 asks "did this reach the public
        /// container"; a source-only match would answer "no" about a move that did.
        /// </summary>
        [Fact]
        public async Task A_move_is_found_under_both_the_source_and_the_destination_container()
        {
            var fake = new FakeStorageService();

            await fake.MoveAsync(
                Constant.StorageContainer.Media, "hike/clip.mp4",
                Constant.StorageContainer.Covers, "hike/cover.mp4");

            Assert.Single(fake.CallsTo(Constant.StorageContainer.Media));

            StorageCall arrived = Assert.Single(fake.CallsTo(Constant.StorageContainer.Covers));
            Assert.Equal("hike/cover.mp4", arrived.DestinationPath);
        }

        /// <summary>
        /// A read URL with no expiry is a leaked object, so the lifetime is required and a
        /// nonsensical one is rejected rather than silently clamped.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task A_read_url_needs_a_positive_lifetime(int seconds)
        {
            var fake = new FakeStorageService();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => fake.CreateReadUrlAsync(
                    Constant.StorageContainer.Media,
                    "hike/clip.mp4",
                    TimeSpan.FromSeconds(seconds)));
        }

        [Fact]
        public async Task A_read_url_records_the_container_and_lifetime()
        {
            var fake = new FakeStorageService();

            await fake.CreateReadUrlAsync(
                Constant.StorageContainer.Media,
                "hike/clip.mp4",
                TimeSpan.FromMinutes(5));

            StorageCall call = fake.SingleCall();
            Assert.Equal(StorageOperation.CreateReadUrl, call.Operation);
            Assert.Equal(Constant.StorageContainer.Media, call.Container);
            Assert.Equal(TimeSpan.FromMinutes(5), call.Lifetime);
        }

        /// <summary>
        /// The three containers of the closed set are distinct names: two features routing to
        /// "the public one" must not silently be the same bucket.
        /// </summary>
        [Fact]
        public void The_container_set_is_three_distinct_names()
        {
            string[] containers =
            [
                Constant.StorageContainer.Covers,
                Constant.StorageContainer.Avatars,
                Constant.StorageContainer.Media,
            ];

            Assert.Equal(3, containers.Distinct().Count());
            Assert.All(containers, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        }
    }
}
