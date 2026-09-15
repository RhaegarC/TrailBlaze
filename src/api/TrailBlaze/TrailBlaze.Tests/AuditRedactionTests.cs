using Microsoft.EntityFrameworkCore;
using TrailBlaze.Model;
using TrailBlaze.Repository;
using TrailBlaze.Tests.TestSupport;

namespace TrailBlaze.Tests
{
    /// <summary>
    /// IM-10 added <see cref="NotAuditedAttribute"/> as an opt-out from the before/after
    /// snapshot. Nothing in the template is annotated with it yet, so this is the only place
    /// the mechanism is exercised at all — which is exactly why it is worth a test rather
    /// than leaving an unrun code path in the audit trail's write path.
    /// </summary>
    public class AuditRedactionTests
    {
        /// <summary>A stand-in entity: one ordinary property, one opted out.</summary>
        private sealed class Probe
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();

            public string? Visible { get; set; }

            [NotAudited]
            public string? Secret { get; set; }
        }

        private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
        {
            public DbSet<Probe> Probes => Set<Probe>();
        }

        private static ProbeContext NewContext() =>
            new(new DbContextOptionsBuilder<ProbeContext>()
                .UseNpgsql(AuditHarness.UnreachableConnectionString)
                .Options);

        /// <summary>
        /// Serialization is called directly rather than through a save: the interceptor only
        /// stages entries for a <c>TrailBlazeContext</c>, and this probe entity exists to carry the
        /// attribute, not to be persisted.
        /// </summary>
        [Fact]
        public void An_opted_out_property_is_left_out_of_the_snapshot()
        {
            using var context = NewContext();
            var probe = new Probe { Visible = "visible", Secret = "secret" };
            context.Probes.Add(probe);

            var interceptor = new AuditSaveChangesInterceptor(new FakeUserContext());
            string json = interceptor.SerializeEntity(context.Entry(probe), isOriginal: false)!;

            Assert.Contains("visible", json);
            Assert.DoesNotContain("secret", json);
            Assert.DoesNotContain(nameof(Probe.Secret), json);
        }

        /// <summary>
        /// The control for the test above. A property needs no attribute to be audited, so an
        /// entity that opts nothing out is captured whole — otherwise "the secret is missing"
        /// could be explained by the serializer dropping properties for some other reason.
        /// </summary>
        [Fact]
        public void A_property_without_the_attribute_is_kept()
        {
            using var context = NewContext();
            var probe = new Probe { Visible = "visible" };
            context.Probes.Add(probe);

            var interceptor = new AuditSaveChangesInterceptor(new FakeUserContext());
            string json = interceptor.SerializeEntity(context.Entry(probe), isOriginal: false)!;

            Assert.Contains(nameof(Probe.Visible), json);
            Assert.Contains(nameof(Probe.Id), json);
        }
    }
}
