namespace TrailBlaze.Service;

using Microsoft.Extensions.Logging;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.Authorization;
using TrailBlaze.Model.DatabaseEntity;

/// <inheritdoc/>
public sealed class ActivityService(
    IDbRepository dbRepository,
    IStorageRepository storageRepository,
    IUploadValidationService uploadValidation,
    IActivityAuthorizationService authorization,
    IUserContextService userContext,
    ILogger<ActivityService> logger) : IActivityService
{
    /// <summary>The form field a refused cover reports its reason under — the name the route reads,
    /// so a client can put the message beside the control that caused it.</summary>
    private const string FileFormField = "file";

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
            Caller caller = await authorization.ResolveAsync();

            if (!caller.IsSignedIn)
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
                CreatedBy = caller.Id,
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
            Caller caller = await authorization.ResolveAsync();
            Activity? activity = await FindAsync(id);

            // An entry the caller may not read is answered as absent rather than forbidden:
            // confirming the id names something is the fact being withheld, so the two answers have
            // to be the same one.
            if (!authorization.CanRead(activity, caller))
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
            Caller caller = await authorization.ResolveAsync();

            int appliedPage = Math.Max(page, 0);
            int appliedSize = pageSize <= 0
                ? Constant.ActivityPaging.DefaultPageSize
                : Math.Min(pageSize, Constant.ActivityPaging.MaxPageSize);

            // Saturating keeps a huge page index a past-the-end request rather than a negative OFFSET.
            int skip = (int)Math.Min((long)appliedPage * appliedSize, int.MaxValue);

            (List<Activity> items, int total) = await dbRepository.GetPageAsync<Activity>(
                authorization.VisibleTo(caller), NewestFirst, skip, appliedSize);

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
            Caller caller = await authorization.ResolveAsync();

            // Read before validating, so an id that names nothing is a 404 whatever the body says.
            Activity? activity = await FindAsync(id);

            if (!authorization.CanRead(activity, caller))
            {
                return ActivityOutcome.NotFound();
            }

            // Then ownership, and also before validating: a caller with no right to edit is not owed
            // a field-by-field answer about an edit they were never going to make. Readable but not
            // theirs is the one answer that is neither 404 nor 400.
            if (!authorization.CanMutate(activity, caller))
            {
                return ActivityOutcome.Forbidden();
            }

            IReadOnlyDictionary<string, string[]> errors = Validate(request);

            if (errors.Count > 0)
            {
                return ActivityOutcome.Rejected(errors);
            }

            ActivityDraft draft = ActivityDraft.From(request);

            // Read before the assignment, so the cover's container can be compared with the one the
            // new type requires.
            string storedType = activity.Type;

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

            await MoveCoverAsync(activity, storedType);

            // Read, changed, written back — never replaced, because UpdateAsync writes every property.
            await dbRepository.UpdateAsync(activity);

            return ActivityOutcome.Completed((await RespondAsync([activity], caller))[0]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Updating activity {ActivityId} failed.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CoverOutcome> UploadCoverAsync(
        string activityId,
        Stream content,
        string? contentType,
        long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            // Refused before the caller is resolved, so a bad image writes nothing and costs no query.
            string? rejection = uploadValidation.ValidateImage(contentType, sizeBytes);

            if (rejection is not null)
            {
                return CoverOutcome.Rejected(
                    new Dictionary<string, string[]> { [FileFormField] = [rejection] });
            }

            Caller caller = await authorization.ResolveAsync();

            if (!caller.IsSignedIn)
            {
                return CoverOutcome.NoCaller();
            }

            Activity? activity = await FindAsync(activityId);

            // An entry this caller cannot read is one they cannot give a face to, and the answer is
            // not-found for the reason the detail read gives: confirming the id names something is
            // the fact being withheld.
            if (!authorization.CanRead(activity, caller))
            {
                return CoverOutcome.NotFound();
            }

            // Readable is not the same as theirs. A cover is the entry's own face rather than a
            // contribution to it, so this is where the two axes part: media is collaborative and a
            // cover is not.
            if (!authorization.CanMutate(activity, caller))
            {
                return CoverOutcome.Forbidden();
            }

            // Derived from the entry's type, which a change across the public line moves the blob
            // for — so the previous cover is in this same container by the time a replace arrives.
            string container = CoverContainerFor(activity.Type);
            string? previous = activity.CoverImageBlobPath;
            string path = CoverPathFor(activityId, contentType!);

            await storageRepository.UploadAsync(container, path, content, contentType!);

            // The row after the bytes, and the old blob after the row. A failure at either step then
            // leaves an object nothing points at, which is inert, rather than a row pointing at
            // nothing, which is a broken image.
            activity.CoverImageBlobPath = path;
            await dbRepository.UpdateAsync(activity);

            if (!string.IsNullOrWhiteSpace(previous) && previous != path)
            {
                await storageRepository.DeleteAsync(container, previous);
            }

            return CoverOutcome.Uploaded(new CoverResponse
            {
                CoverImageUrl = await CoverUrlAsync(path, activity.Type),
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Uploading a cover for activity {ActivityId} failed.", activityId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ActivityOutcome> DeleteAsync(string id)
    {
        try
        {
            Caller caller = await authorization.ResolveAsync();
            Activity? activity = await FindAsync(id);

            if (!authorization.CanRead(activity, caller))
            {
                return ActivityOutcome.NotFound();
            }

            if (!authorization.CanMutate(activity, caller))
            {
                return ActivityOutcome.Forbidden();
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
    private async Task<List<ActivityResponse>> RespondAsync(List<Activity> activities, Caller caller)
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
                CoverImageUrl = string.IsNullOrWhiteSpace(activity.CoverImageBlobPath)
                    ? null
                    : await CoverUrlAsync(activity.CoverImageBlobPath, activity.Type),
                MediaCount = counts.GetValueOrDefault(activity.Id),
                CreatorDisplayName = activity.CreatedBy is { } creator ? names.GetValueOrDefault(creator) : null,

                // The id is disclosed to a caller who has one, and to nobody else: an anonymous
                // payload names the creator by display name only (Decision #30).
                CreatedByUserId = caller.IsSignedIn ? activity.CreatedBy : null,
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

    /// <summary>The URL a cover is reached by, from the container its entry's type requires
    /// (Decision #29) — the container *is* the public/private answer.</summary>
    private async Task<string> CoverUrlAsync(string path, string type) =>
        CoverContainerFor(type) == Constant.StorageContainer.Covers
            ? storageRepository.CreatePublicUrl(Constant.StorageContainer.Covers, path).ToString()
            : (await storageRepository.CreateReadUrlAsync(
                Constant.StorageContainer.Media, path, Constant.CoverUrl.SasLifetime)).ToString();

    /// <summary>The container a cover belongs in: the public <c>covers</c> for a <c>Public</c> entry,
    /// the private <c>media</c> for a <c>Shared</c> or <c>Private</c> one. Both private types share a
    /// container, which is why a change between them has nothing to move.</summary>
    private static string CoverContainerFor(string type) =>
        type == Constant.ActivityType.Public
            ? Constant.StorageContainer.Covers
            : Constant.StorageContainer.Media;

    /// <summary>The activity's folder, then a fresh name, so a client that cached the previous cover
    /// is never served these bytes under the old path.</summary>
    private string CoverPathFor(string activityId, string contentType) =>
        $"{activityId}/{Guid.NewGuid():N}{uploadValidation.FileExtensionFor(contentType)}";

    /// <summary>
    /// Moves the cover when an edit crosses the public line, copying it to the container the new type
    /// requires and deleting the copy the old one left.
    /// </summary>
    /// <remarks>
    /// Before the row is written and never after. The two failures are not symmetric: a move that
    /// failed after the save would leave a now-<c>Private</c> entry's cover sitting in the public
    /// container, where it stays fetchable by anyone who ever held the link — the disclosure this
    /// exists to prevent — whereas a save that failed after the move leaves a broken link and nothing
    /// readable.
    /// </remarks>
    private async Task MoveCoverAsync(Activity activity, string storedType)
    {
        // Nothing to relocate when there is no cover, or when both containers are private: Shared and
        // Private share `media`, so a change between them is not a change of audience.
        if (string.IsNullOrWhiteSpace(activity.CoverImageBlobPath)
            || CoverContainerFor(storedType) == CoverContainerFor(activity.Type))
        {
            return;
        }

        activity.CoverImageBlobPath = await storageRepository.MoveAsync(
            CoverContainerFor(storedType),
            activity.CoverImageBlobPath,
            CoverContainerFor(activity.Type),
            activity.CoverImageBlobPath);
    }
}
