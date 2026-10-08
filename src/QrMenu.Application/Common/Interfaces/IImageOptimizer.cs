namespace QrMenu.Application.Common.Interfaces;

/// <summary>What an uploaded image is for; decides how large it is kept.</summary>
public enum ImagePurpose
{
    /// <summary>Dish photo: shown as a thumbnail and on the item page.</summary>
    Item,

    /// <summary>Full-screen background photo behind the menu and admin panel.</summary>
    Background,

    /// <summary>Restaurant logo.</summary>
    Logo
}

public record OptimizedImage(byte[] Content, string Extension, string ContentType, int Width, int Height);

/// <summary>
/// Makes an upload safe and light before it is stored: checks it really is an image, applies the phone's
/// rotation, removes hidden data (GPS location, camera details), scales it down to what the screen needs and
/// re-saves it at a quality that looks the same.
/// </summary>
public interface IImageOptimizer
{
    /// <exception cref="Exceptions.ConflictException">The file is not a readable JPEG, PNG or WEBP image.</exception>
    Task<OptimizedImage> OptimizeAsync(Stream source, ImagePurpose purpose, CancellationToken ct = default);

    /// <summary>A small copy of an already stored image (longest side at most <paramref name="maxSide"/>), for lists.</summary>
    Task<OptimizedImage> ThumbnailAsync(Stream source, int maxSide, CancellationToken ct = default);
}
