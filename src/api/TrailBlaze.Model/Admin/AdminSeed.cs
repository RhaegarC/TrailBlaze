namespace TrailBlaze.Model.Admin;

/// <summary>
/// The one administrator a deployment configures: the Entra object id the seeded row answers
/// to, and the name to give it if it does not exist yet.
/// </summary>
/// <remarks>
/// <para>
/// A type rather than two strings threaded through the composition root, because the pair is
/// what the rule is about — "exactly one administrator, identified by object id" — and two
/// adjacent <c>string</c> parameters are two arguments that can be passed the wrong way round
/// without a compiler noticing.
/// </para>
/// <para>
/// There is deliberately no <c>Email</c> here. The PRD's open column decision did not add one
/// as a configured value, and the seeded row's email arrives the ordinary way if it ever
/// arrives: through a token whose caller the row already belongs to.
/// </para>
/// </remarks>
/// <param name="EntraObjectId">The <c>users</c> key the administrator signs in as.</param>
/// <param name="DisplayName">The name stored if the row is created. An existing row keeps its
/// own — see <c>AdminSeedingService</c>.</param>
public sealed record AdminSeed(string EntraObjectId, string DisplayName);
