namespace TrailBlaze.Service;

using Microsoft.Extensions.Logging;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Model.Media;

/// <inheritdoc/>
public sealed class MediaService(
    IDbRepository dbRepository,
    IStorageRepository storageRepository,
    IActivityService activityService,
    IUserContextService userContext,
    IUploadValidationService uploadValidation,
    ILogger<MediaService> logger) : IMediaService
{
    /// <summary>The form field a refused upload reports its reason under — the name the route reads,
    /// so a client can put the message beside the control that caused it.</summary>
    private const string FileFormField = "file";

    /// <inheritdoc/>
    public async Task<MediaOutcome> UploadAsync(
        string activityId,
        Stream content,
        string? contentType,
        long sizeBytes,
        string? fileName)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            string? kind = uploadValidation.MediaKindOf(contentType);

            if (kind is null)
            {
                return Rejected(Constant.Message.MediaTypeNotAllowed);
            }

            // The cap is the kind's own, and the comparison is the shared service's: an image is held
            // to 10 MB and a video to 200, and neither number is restated here.
            string? rejection = kind == Constant.MediaKind.Image
                ? uploadValidation.ValidateImage(contentType, sizeBytes)
                : uploadValidation.ValidateVideo(contentType, sizeBytes);

            // Refused before the caller is resolved, so a bad upload writes nothing and costs no query.
            if (rejection is not null)
            {
                return Rejected(rejection);
            }

            string? caller = userContext.EntraObjectId;

            if (string.IsNullOrWhiteSpace(caller))
            {
                return MediaOutcome.NoCaller();
            }

            Activity? activity = await dbRepository.GetAsync<Activity>(row => row.Id == activityId);

            // One answer for an activity that does not exist and one this caller may not read: media
            // is collaborative, so the gate is the read rule and not ownership (Decision #27).
            if (activity is null || !activityService.CanRead(activity, caller))
            {
                return MediaOutcome.NotFound();
            }

            User? uploader = await dbRepository.GetAsync<User>(row => row.Id == caller);

            string path = MediaPathFor(activityId, contentType!);

            var media = new Media
            {
                ActivityId = activityId,

                // From the request in flight, never from a body: an upload cannot be attributed to
                // someone else, exactly as an activity's creator cannot.
                CreatedBy = caller,
                Kind = kind!,
                BlobPath = path,
                ContentType = contentType!,
                SizeBytes = sizeBytes,
                OriginalFileName = fileName ?? string.Empty,
            };

            // The row before the bytes, counted and inserted as one step, so a caller at the cap is
            // turned away before any blob exists.
            bool stored = await dbRepository.CreateIfUnderAsync(
                media, row => row.ActivityId == activityId, Constant.MediaLimit.PerActivity);

            if (!stored)
            {
                return MediaOutcome.LimitReached();
            }

            try
            {
                await storageRepository.UploadAsync(
                    Constant.StorageContainer.Media, path, content, contentType!);
            }
            catch
            {
                // The row is committed and the bytes never landed, which a reader would see as an item
                // whose fetch fails. Undone rather than left, since the row is the visible half.
                try
                {
                    await dbRepository.DeleteAsync<Media>([media.Id]);
                }
                catch (Exception undo)
                {
                    // Best-effort: the request is already failing, and an undo that threw on top of
                    // that would replace the real cause with its own.
                    logger.LogError(undo, "Undoing media row {MediaId} after a failed upload failed.", media.Id);
                }

                throw;
            }

            return MediaOutcome.Uploaded(ToResponse(media, uploader?.DisplayName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Uploading media to activity {ActivityId} failed.", activityId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<MediaListing> ListAsync(string activityId)
    {
        try
        {
            string? caller = userContext.EntraObjectId;

            // The route carries [Authorize], so a blank caller cannot arrive. Answering not-found
            // rather than reading is what keeps this surface from becoming a way to read without a
            // token, should the route's attribute ever change.
            if (string.IsNullOrWhiteSpace(caller))
            {
                return MediaListing.NotFound();
            }

            Activity? activity = await dbRepository.GetAsync<Activity>(row => row.Id == activityId);

            if (activity is null || !activityService.CanRead(activity, caller))
            {
                return MediaListing.NotFound();
            }

            List<Media> items =
                await dbRepository.GetListAsync<Media>(row => row.ActivityId == activityId);

            // One read for the whole listing rather than one per uploader: the model declares no
            // relationship, so the names are joined here rather than followed as a navigation.
            string[] uploaderIds = [.. items.Select(item => item.CreatedBy).OfType<string>().Distinct()];

            List<User> uploaders = uploaderIds.Length == 0
                ? []
                : await dbRepository.GetListAsync<User>(user => uploaderIds.Contains(user.Id));

            Dictionary<string, string?> names =
                uploaders.ToDictionary(user => user.Id, user => user.DisplayName);

            return MediaListing.Of(
                [.. items
                    .OrderBy(item => item.CreatedOn)
                    .ThenBy(item => item.Id)
                    .Select(item => ToResponse(item, names.GetValueOrDefault(item.CreatedBy ?? string.Empty)))]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Listing media of activity {ActivityId} failed.", activityId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<MediaOutcome> DeleteAsync(string mediaId)
    {
        try
        {
            string? caller = userContext.EntraObjectId;

            if (string.IsNullOrWhiteSpace(caller))
            {
                return MediaOutcome.NoCaller();
            }

            Media? media = await dbRepository.GetAsync<Media>(row => row.Id == mediaId);

            if (media is null)
            {
                return MediaOutcome.NotFound();
            }

            Activity? activity = await dbRepository.GetAsync<Activity>(row => row.Id == media.ActivityId);

            // Three principals and no fourth (Decision #27): the contributor, the owner of the entry
            // the item sits on, and an administrator. The administrator is not among them yet --
            // nothing can read a role today -- so a caller who is one is judged as an ordinary user.
            bool permitted = media.CreatedBy == caller
                || activity?.CreatedBy == caller;

            if (!permitted)
            {
                return MediaOutcome.Forbidden();
            }

            // Row first, then blob: this order can at worst leave an unreferenced blob, which is inert.
            await dbRepository.DeleteAsync<Media>([media.Id]);
            await storageRepository.DeleteAsync(Constant.StorageContainer.Media, media.BlobPath);

            return MediaOutcome.Deleted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deleting media {MediaId} failed.", mediaId);
            throw;
        }
    }

    /// <summary>A refusal keyed by the form field, so it arrives in the shape a client already reads
    /// for a body that did not bind.</summary>
    private static MediaOutcome Rejected(string reason) =>
        MediaOutcome.Rejected(new Dictionary<string, string[]> { [FileFormField] = [reason] });

    /// <summary>Where a newly uploaded item is stored: the activity's folder, then a fresh name, so a
    /// client that cached a path is never served the previous bytes under it.</summary>
    private string MediaPathFor(string activityId, string contentType) =>
        $"{activityId}/{Guid.NewGuid():N}{uploadValidation.FileExtensionFor(contentType)}";

    /// <summary>The stored item as the client sees it. It carries no path, by design: the bytes are
    /// reached through feature 07, and this is what says there are any.</summary>
    private static MediaResponse ToResponse(Media media, string? uploaderDisplayName) => new()
    {
        Id = media.Id,
        Kind = media.Kind,
        ContentType = media.ContentType,
        SizeBytes = media.SizeBytes,
        OriginalFileName = media.OriginalFileName,
        CreatedOn = media.CreatedOn,
        UploadedByUserId = media.CreatedBy ?? string.Empty,
        UploaderDisplayName = uploaderDisplayName,
    };
}
