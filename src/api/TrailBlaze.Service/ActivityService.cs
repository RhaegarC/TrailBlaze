namespace TrailBlaze.Service;

using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.DatabaseEntity;

/// <inheritdoc/>
public sealed class ActivityService(
    IDbRepository dbRepository,
    IUserContextService userContext,
    IActivityValidationService validation) : IActivityService
{
    /// <inheritdoc/>
    public async Task<ActivityOutcome> CreateAsync(CreateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Refused before the caller is resolved, so a bad body writes nothing and costs no read.
        IReadOnlyDictionary<string, string[]> errors = validation.Validate(request);

        if (errors.Count > 0)
        {
            return ActivityOutcome.Rejected(errors);
        }

        // The route carries [Authorize], so this is a token that validated without identifying
        // anyone. There is no one to attribute an entry to, and an unattributed entry is one
        // nobody may edit and nobody may be shown as having written.
        string? caller = userContext.EntraObjectId;

        if (string.IsNullOrWhiteSpace(caller))
        {
            return ActivityOutcome.NoCaller();
        }

        ActivityDraft draft = ActivityDraft.From(request);

        var activity = new Activity
        {
            Title = draft.Title,
            Location = draft.Location,
            ActivityDate = draft.ActivityDate,
            Description = draft.Description,
            Type = draft.Type,

            // Attribution is read from the request in flight and never from the body, which has
            // no property for it. Id, CreatedOn and CreatedBy are left to EntityBase and the
            // audit interceptor.
            CreatedByUserId = caller,
        };

        await dbRepository.CreateAsync(activity);

        return ActivityOutcome.Completed(ToResponse(activity));
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> GetAsync(string id)
    {
        Activity? activity = await FindAsync(id);

        return activity is null
            ? ActivityOutcome.NotFound()
            : ActivityOutcome.Completed(ToResponse(activity));
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> UpdateAsync(string id, UpdateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read before validating, so an id that names nothing is a 404 whatever the body says:
        // the alternative answers "your date is malformed" about an entry that does not exist.
        Activity? activity = await FindAsync(id);

        if (activity is null)
        {
            return ActivityOutcome.NotFound();
        }

        IReadOnlyDictionary<string, string[]> errors = validation.Validate(request);

        if (errors.Count > 0)
        {
            return ActivityOutcome.Rejected(errors);
        }

        ActivityDraft draft = ActivityDraft.From(request);

        activity.Title = draft.Title;
        activity.Location = draft.Location;
        activity.ActivityDate = draft.ActivityDate;
        activity.Description = draft.Description;

        // The one field an omission does not default. An absent type leaves the stored value
        // alone rather than falling back to Public: a visibility change is a disclosure, so it
        // has to be asked for. Falling back here would publish an entry whenever a client sent
        // a body that predated the field.
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            activity.Type = draft.Type;
        }

        // Read, changed, written back — never replaced with a fresh entity. UpdateAsync writes
        // every property, so one built from the request alone would blank `CreatedOn`.
        await dbRepository.UpdateAsync(activity);

        return ActivityOutcome.Completed(ToResponse(activity));
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> DeleteAsync(string id)
    {
        Activity? activity = await FindAsync(id);

        if (activity is null)
        {
            return ActivityOutcome.NotFound();
        }

        await dbRepository.DeleteAsync<Activity>([id]);

        return ActivityOutcome.Deleted();
    }

    /// <summary>The live row, or null for an id that is unknown or already deleted — the
    /// soft-delete filter makes the second case indistinguishable from the first, deliberately.
    /// </summary>
    private async Task<Activity?> FindAsync(string id) =>
        await dbRepository.GetAsync<Activity>(row => row.Id == id);

    private static ActivityResponse ToResponse(Activity activity) => new()
    {
        Id = activity.Id,
        Title = activity.Title,
        Location = activity.Location,
        ActivityDate = activity.ActivityDate,
        Description = activity.Description,
        Type = activity.Type,
        CoverImageBlobPath = activity.CoverImageBlobPath,
    };
}
