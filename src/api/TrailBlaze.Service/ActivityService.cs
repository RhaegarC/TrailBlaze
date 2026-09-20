namespace TrailBlaze.Service;

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.DatabaseEntity;

/// <inheritdoc/>
public sealed class ActivityService(
    IDbRepository dbRepository,
    IStorageRepository storageRepository,
    IUserContextService userContext,
    ILogger<ActivityService> logger) : IActivityService
{
    /// <inheritdoc/>
    public Expression<Func<Activity, bool>> VisibleTo(string? caller) =>
        string.IsNullOrWhiteSpace(caller)
            ? activity => activity.Type == Constant.ActivityType.Public
            : activity => activity.Type == Constant.ActivityType.Public
                || activity.Type == Constant.ActivityType.Shared
                || activity.CreatedBy == caller;

    /// <inheritdoc/>
    public bool CanRead(Activity activity, string? caller)
    {
        ArgumentNullException.ThrowIfNull(activity);

        // Compiled from the expression the queries use rather than restated in C#: a second copy of
        // an access rule is a second rule.
        return VisibleTo(caller).Compile()(activity);
    }

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

        // An absent type is a caller who supplied none, which the default answers rather than a rejection.
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

            // The route carries [Authorize], so this is a token that validated without naming anyone,
            // and an entry with no author is one nobody may edit.
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

                // From the request in flight, never the body, which has no property for it.
                CreatedBy = caller,
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

            // Saturating keeps a huge page index a past-the-end request rather than a negative OFFSET.
            int skip = (int)Math.Min((long)appliedPage * appliedSize, int.MaxValue);

            (List<Activity> items, int total) = await dbRepository.GetPageAsync<Activity>(
                VisibleTo(caller), NewestFirst, skip, appliedSize);

            logger.LogInformation(
                "Listed {Count} of {Total} activities for page {Page}.", items.Count, total, appliedPage);

            return new ActivityPage
            {
                // A path is the address a SAS is minted for, so a token-less caller is shown none.
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
            // Read before validating, so an id that names nothing is a 404 whatever the body says.
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

            // The one field an omission does not default: a visibility change is a disclosure, so it
            // has to be asked for rather than fall out of a body that predated the field.
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                activity.Type = draft.Type;
            }

            // Read, changed, written back — never replaced, because UpdateAsync writes every property.
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

            await RemoveMediaAsync(id);
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
    /// Takes an activity's media with it, whichever uploader contributed each item.
    /// </summary>
    /// <remarks>
    /// The model declares no foreign keys, so nothing cascades this: without it the rows would
    /// outlive their activity, holding blobs nothing can reach.
    /// </remarks>
    /// <param name="activityId">The activity being deleted.</param>
    private async Task RemoveMediaAsync(string activityId)
    {
        List<Media> items =
            await dbRepository.GetListAsync<Media>(row => row.ActivityId == activityId);

        if (items.Count == 0)
        {
            return;
        }

        await dbRepository.DeleteAsync<Media>([.. items.Select(item => item.Id)]);

        // Rows first, then blobs: this order leaves at worst an unreferenced blob, which nothing
        // shows, where the reverse leaves a row naming bytes that are already gone.
        foreach (Media item in items)
        {
            await storageRepository.DeleteAsync(Constant.StorageContainer.Media, item.BlobPath);
        }
    }

    /// <summary>The live row, or null for an id that is unknown or already deleted — the soft-delete
    /// filter makes the second case indistinguishable from the first, deliberately.</summary>
    private async Task<Activity?> FindAsync(string id) =>
        await dbRepository.GetAsync<Activity>(row => row.Id == id);

    /// <summary>The id is the tie-break, so the order is total and a row cannot appear on two pages
    /// or fall between them.</summary>
    private static IOrderedQueryable<Activity> NewestFirst(IQueryable<Activity> query) =>
        query.OrderByDescending(activity => activity.CreatedOn)
            .ThenByDescending(activity => activity.Id);

    /// <summary>Whitespace is absence, not a value.</summary>
    private static string? Missing(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? message : null;

    /// <summary>Inclusive, and measured on the trimmed value — the length the column must hold.</summary>
    private static string? TooLong(string? value, int cap, string message) =>
        value is not null && value.Trim().Length > cap ? message : null;

    /// <summary>One field, two possible objections, so a blank field one character too long reports
    /// both.</summary>
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

    /// <summary>The response shape, with the cover path withheld from a caller who has no token.</summary>
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
