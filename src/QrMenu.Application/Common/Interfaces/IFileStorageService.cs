namespace QrMenu.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>Stores already-processed bytes under a new random name and returns its public URL.</summary>
    /// <param name="extension">Including the dot, e.g. ".jpg". Chosen by the server, never taken from the upload.</param>
    Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct = default);
}
