namespace TrailBlaze.Service;

using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.DatabaseEntity;

/// <inheritdoc/>
public sealed class ActivityService(
    IDbRepository dbRepository,
    IUserContextService userContext,
    ILogger<ActivityService> logger) : IActivityService
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string[]> Validate(IActivityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new Dictionary<string, string[]>();

        AddIf(errors, nameof(input.Title), Missing(input.Title, Constant.Message.TitleRequired));
        AddIf(errors, nameof(input.Title), TooLong(input.Title, Constant.ActivityField.TitleLength, Constant.Message.TitleTooLong));

        AddIf(errors, nameof(input.Location), Missing(input.Location, Constant.Message.LocationRequired));
        AddIf(errors, nameof(input.Location), TooLong(input.Location, Constant.ActivityField.LocationLength, Constant.Message.LocationTooLong));

        if (input.ActivityDate is null)
        {
            errors[nameof(input.ActivityDate)] = [Constant.Message.ActivityDateRequired];
        }

        // An absent type is a caller who supplied none, which is not a value to reject: the
        // default answers it. Only a value that names something is checked against the set.
        if (!ActivityDraft.IsKnownType(input.Type))
        {
            errors[nameof(input.Type)] = [Constant.Message.TypeNotAllowed];
        }

        return errors;
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> CreateAsync(CreateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Refused before the caller is resolved, so a bad body writes nothing and costs no read.
            IReadOnlyDictionary<string, string[]> errors = Validate(request);

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

            return ActivityOutcome.Completed(ToResponse(activity, includeCoverPath: true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating an activity failed for caller {Caller}.", userContext.EntraObjectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> GetAsync(string id)
    {
        try
        {
            Activity? activity = await FindAsync(id);

            return activity is null
                ? ActivityOutcome.NotFound()
                : ActivityOutcome.Completed(ToResponse(activity, includeCoverPath: true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reading activity {ActivityId} failed.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ActivityPage> GetPageAsync(int page, int pageSize)
    {
        try
        {
            string? caller = userContext.EntraObjectId;

            int appliedPage = Math.Max(page, 0);
            int appliedSize = pageSize <= 0
                ? Constant.ActivityPaging.DefaultPageSize
                : Math.Min(pageSize, Constant.ActivityPaging.MaxPageSize);

            // A page index this large is a past-the-end request rather than an overflow:
            // saturating the skip keeps the answer an empty page instead of a negative OFFSET.
            int skip = (int)Math.Min((long)appliedPage * appliedSize, int.MaxValue);

            (List<Activity> items, int total) = await dbRepository.GetPageAsync<Activity>(
                VisibleToCaller(caller), NewestFirst, skip, appliedSize);

            logger.LogInformation(
                "Listed {Count} of {Total} activities for page {Page}.", items.Count, total, appliedPage);

            return new ActivityPage
            {
                // A caller with no token is shown no blob path at all: a cover may live in the
                // private container, and a path is the address a SAS is minted for.
                Items = [.. items.Select(item => ToResponse(item, includeCoverPath: caller is not null))],
                Page = appliedPage,
                PageSize = appliedSize,
                Total = total,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Listing activities failed. Page {Page}, page size {PageSize}.", page, pageSize);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> UpdateAsync(string id, UpdateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Read before validating, so an id that names nothing is a 404 whatever the body says:
            // the alternative answers "your date is malformed" about an entry that does not exist.
            Activity? activity = await FindAsync(id);

            if (activity is null)
            {
                return ActivityOutcome.NotFound();
            }

            IReadOnlyDictionary<string, string[]> errors = Validate(request);

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

            return ActivityOutcome.Completed(ToResponse(activity, includeCoverPath: true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Updating activity {ActivityId} failed.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> DeleteAsync(string id)
    {
        try
        {
            Activity? activity = await FindAsync(id);

            if (activity is null)
            {
                return ActivityOutcome.NotFound();
            }

            await dbRepository.DeleteAsync<Activity>([id]);

            return ActivityOutcome.Deleted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deleting activity {ActivityId} failed.", id);
            throw;
        }
    }

    /// <summary>
    /// The live row, or null for an id that is unknown or already deleted — the soft-delete filter
    /// makes the second case indistinguishable from the first, deliberately.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private async Task<Activity?> FindAsync(string id) =>
        await dbRepository.GetAsync<Activity>(row => row.Id == id);

    /// <summary>
    /// The entries a caller may read: a token-less caller sees the public ones, a signed-in caller
    /// adds the shared ones and their own private ones.
    /// </summary>
    /// <remarks>
    /// No admin branch: <c>IUserContextService</c> carries no role, and reading one is the
    /// permission work feature 09 owns. Until then an administrator pages what a user pages.
    /// </remarks>
    /// <param name="caller"></param>
    /// <returns></returns>
    private static Expression<Func<Activity, bool>> VisibleToCaller(string? caller) =>
        string.IsNullOrWhiteSpace(caller)
            ? activity => activity.Type == Constant.ActivityType.Public
            : activity => activity.Type == Constant.ActivityType.Public
                || activity.Type == Constant.ActivityType.Shared
                || activity.CreatedByUserId == caller;

    /// <summary>
    /// Ordering by creation, newest first, with the id as a tie-break so the order is total and a
    /// row cannot appear on two pages or fall between them.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    private static IOrderedQueryable<Activity> NewestFirst(IQueryable<Activity> query) =>
        query.OrderByDescending(activity => activity.CreatedOn)
            .ThenByDescending(activity => activity.Id);

    /// <summary>
    /// Whitespace is absence, not a value: it reaches the column as empty once trimmed.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private static string? Missing(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? message : null;

    /// <summary>
    /// Inclusive, and measured on the trimmed value — the length the column must hold.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="cap"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private static string? TooLong(string? value, int cap, string message) =>
        value is not null && value.Trim().Length > cap ? message : null;

    /// <summary>
    /// One field, two possible objections, so a blank field one character too long reports both.
    /// </summary>
    /// <param name="errors"></param>
    /// <param name="field"></param>
    /// <param name="message"></param>
    private static void AddIf(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is null)
        {
            return;
        }

        errors[field] = errors.TryGetValue(field, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }

    /// <summary>
    /// The response shape, with the cover path withheld from a caller who has no token.
    /// </summary>
    /// <param name="activity"></param>
    /// <param name="includeCoverPath"></param>
    /// <returns></returns>
    private static ActivityResponse ToResponse(Activity activity, bool includeCoverPath) => new()
    {
        Id = activity.Id,
        Title = activity.Title,
        Location = activity.Location,
        ActivityDate = activity.ActivityDate,
        Description = activity.Description,
        Type = activity.Type,
        CoverImageBlobPath = includeCoverPath ? activity.CoverImageBlobPath : null,
    };
}
