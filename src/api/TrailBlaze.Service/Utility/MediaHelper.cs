namespace TrailBlaze.Service.Utility;

using ImageMagick;
using TrailBlaze.Model;

public static class MediaHelper
{
    /// <summary>
    /// Create a new stream with smaller size for thumbnail
    /// </summary>
    /// <param name="content">Original content</param>
    /// <returns>Thumbnail contents</returns>
    public static async Task<Stream> GetThumbnailStreamAsync(Stream content)
    {
        content.Position = 0;
        string? quality = Environment.GetEnvironmentVariable(Constant.ConfigKey.ThumbnailQuality);
        using var webpStream = new MemoryStream();
        using var image = new MagickImage(content);
        image.AutoOrient();
        image.Format = MagickFormat.WebP;
        image.Quality = Convert.ToUInt32(quality);
        await image.WriteAsync(webpStream);
        return webpStream;
    }
}