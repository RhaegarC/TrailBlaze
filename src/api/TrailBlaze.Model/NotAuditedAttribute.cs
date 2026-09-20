namespace TrailBlaze.Model;

/// <summary>
/// Excludes a property from the audit snapshots. Audit rows typically have looser access
/// control than the tables they describe, so a credential, token or other personal value
/// copied into one becomes readable by people who cannot read the source table — and it
/// stays readable for as long as the history is kept.
/// </summary>
/// <remarks>
/// Nothing in this template is annotated yet: the only entity, <c>User</c>, holds no
/// secret. Annotate a property the moment it becomes one — a password hash, a refresh
/// token, a government id — rather than waiting for a leak to point it out.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class NotAuditedAttribute : Attribute
{
}
