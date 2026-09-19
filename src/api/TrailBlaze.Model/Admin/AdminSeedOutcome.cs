namespace TrailBlaze.Model.Admin;

/// <summary>
/// What one seeding run did, or found already done.
/// </summary>
/// <remarks>
/// <para>
/// Returned rather than reduced to a boolean because the three answers mean different things to
/// whoever reads the log, and only one of them is a change. <see cref="Inserted"/> is a fresh
/// database finding its administrator; <see cref="Promoted"/> is a deploy that has just
/// appointed one who was already a user, which is the case an operator is most likely to be
/// looking for after the fact; <see cref="AlreadyAdmin"/> is every subsequent start, and its
/// being distinguishable is what makes "seeding ran and wrote nothing" an assertion rather than
/// an inference.
/// </para>
/// <para>
/// It is also the whole of what the caller gets. There is no failure member, because a run that
/// could not reach the database throws rather than answering — the tolerance for that lives one
/// level out, in the hosting, where the surviving-the-start question belongs.
/// </para>
/// </remarks>
public enum AdminSeedOutcome
{
    /// <summary>The row did not exist and was created with <c>Role = Admin</c>.</summary>
    Inserted,

    /// <summary>The row existed, was not an administrator, and its <c>Role</c> was the only
    /// column changed.</summary>
    Promoted,

    /// <summary>The row existed and already held <c>Role = Admin</c>. Nothing was written.</summary>
    AlreadyAdmin,
}
