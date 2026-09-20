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

            return ActivityOutcome.Completed((await RespondAsync([activity], caller))[0]);
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
            string? caller = userContext.EntraObjectId;
            Activity? activity = await FindAsync(id);

            // An entry the caller may not read is answered as absent rather than forbidden:
            // confirming the id names something is the fact being withheld, so the two answers have
            // to be the same one. The admin branch would read a role, and no layer exposes one yet —
            // it lands with feature 09, and until then an admin reads what a user reads.
            if (activity is null || !CanRead(activity, caller))
            {
                return ActivityOutcome.NotFound();
            }

            return ActivityOutcome.Completed((await RespondAsync([activity], caller))[0]);
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
                Items = await RespondAsync(items, caller),
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

            return ActivityOutcome.Completed(
                (await RespondAsync([activity], userContext.EntraObjectId))[0]);
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

            // Its media rows and blobs are left standing: the delete is soft, so the activity can be
            // restored and its media with it.
            await dbRepository.DeleteAsync<Activity>([id]);

            return ActivityOutcome.Deleted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deleting activity {ActivityId} failed.", id);
            throw;
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

    /// <summary>Every read's response goes through here, so the two reads cannot grow different
    /// shapes and the name join and count are one query each rather than one per item.</summary>
    private async Task<List<ActivityResponse>> RespondAsync(List<Activity> activities, string? caller)
    {
        if (activities.Count == 0)
        {
            return [];
        }

        Dictionary<string, string?> names = await CreatorNamesAsync(activities);
        Dictionary<string, int> counts = await MediaCountsAsync(activities);

        var responses = new List<ActivityResponse>(activities.Count);

        foreach (Activity activity in activities)
        {
            responses.Add(new ActivityResponse
            {
                Id = activity.Id,
                Title = activity.Title,
                Location = activity.Location,
                ActivityDate = activity.ActivityDate,
                Description = activity.Description,
                Type = activity.Type,
                CoverImageUrl = await CoverUrlAsync(activity),
                MediaCount = counts.GetValueOrDefault(activity.Id),
                CreatorDisplayName = activity.CreatedBy is { } creator ? names.GetValueOrDefault(creator) : null,
                CreatedByUserId = caller is null ? null : activity.CreatedBy,
            });
        }

        return responses;
    }

    /// <summary>The creator is named, not identified: the display name is all a caller learns.</summary>
    private async Task<Dictionary<string, string?>> CreatorNamesAsync(List<Activity> activities)
    {
        string[] creatorIds = [.. activities.Select(activity => activity.CreatedBy).OfType<string>().Distinct()];

        List<User> creators = await dbRepository.GetListAsync<User>(user => creatorIds.Contains(user.Id));

        return creators.ToDictionary(user => user.Id, user => user.DisplayName);
    }

    /// <summary>Counted, never loaded: the caller receives a number and no row.</summary>
    private async Task<Dictionary<string, int>> MediaCountsAsync(List<Activity> activities)
    {
        string[] activityIds = [.. activities.Select(activity => activity.Id)];

        return await dbRepository.CountByAsync<Media>(
            media => activityIds.Contains(media.ActivityId),
            media => media.ActivityId);
    }

    /// <summary>The cover's URL, or null for an entry that has none. The container follows the
    /// activity's visibility (Decision #29), and the container *is* the public/private answer.</summary>
    private async Task<string?> CoverUrlAsync(Activity activity)
    {
        if (string.IsNullOrWhiteSpace(activity.CoverImageBlobPath))
        {
            return null;
        }

        if (activity.Type == Constant.ActivityType.Public)
        {
            return storageRepository
                .CreatePublicUrl(Constant.StorageContainer.Covers, activity.CoverImageBlobPath)
                .ToString();
        }

        Uri signed = await storageRepository.CreateReadUrlAsync(
            Constant.StorageContainer.Media, activity.CoverImageBlobPath, Constant.CoverUrl.SasLifetime);

        return signed.ToString();
    }
}
