namespace EventEaseBooking.Web.Services;

public interface IBlobStorageService
{
    // Throws ArgumentException with a user-facing message if the file fails
    // type/size checks. Called before any DB write so a bad image never leaves
    // a Venue/Event half-created.
    void ValidateFile(IFormFile file);

    // Uploads under "{folder}/{entityId}-{guid}{extension}" and returns the
    // blob's public URL. Re-validates internally, so it's safe to call on its own.
    Task<string> UploadAsync(IFormFile file, string folder, int entityId);

    // No-ops for null/empty URLs and for URLs that aren't blobs in our own
    // container (the Part 1 placehold.co placeholders on un-edited seed rows).
    Task DeleteAsync(string? blobUrl);
}
