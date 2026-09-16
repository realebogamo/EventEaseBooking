using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace EventEaseBooking.Web.Services;

// One container ("eventease-images" by default) rather than one per entity
// type — simpler to provision/secure, and the "venues/"/"events/" folder
// prefix is enough to keep the two image sets apart.
//
// Container access level: Blob (anonymous read on individual blobs, no
// container listing). Venue/Event images are meant to be publicly viewable
// in <img> tags without SAS token plumbing, but the container itself isn't
// browsable — someone would need the exact blob name (a GUID) to fetch one.
public class BlobStorageService : IBlobStorageService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    // Lazy: VenuesController/EventsController inject this service for every
    // action (including plain Index/Details), so connecting eagerly in the
    // constructor would break unrelated pages whenever Blob Storage isn't
    // configured yet. The connection is only required once an upload/delete
    // is actually attempted.
    //
    // PublicationOnly, not the default ExecutionAndPublication: this service
    // is a singleton, and the default mode caches a thrown exception forever
    // — one transient failure (or fixing a config mistake without restarting)
    // would otherwise break uploads for the rest of the app's lifetime.
    // PublicationOnly retries the factory on every failed access instead.
    private readonly Lazy<BlobContainerClient> _container;

    public BlobStorageService(IConfiguration configuration)
    {
        _container = new Lazy<BlobContainerClient>(() =>
        {
            var connectionString = configuration.GetConnectionString("BlobStorage")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:BlobStorage is not configured. Set it via 'dotnet user-secrets set' " +
                    "locally or an App Service connection string in Azure — never in appsettings.json.");
            var containerName = configuration["BlobStorage:ContainerName"] ?? "eventease-images";

            var container = new BlobContainerClient(connectionString, containerName);
            container.CreateIfNotExists(PublicAccessType.Blob);
            return container;
        }, LazyThreadSafetyMode.PublicationOnly);
    }

    public void ValidateFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException("Only .jpg, .jpeg, .png and .webp images are allowed.");

        if (file.Length is 0 or > MaxFileSizeBytes)
            throw new ArgumentException("Image must be larger than 0 bytes and no more than 5 MB.");
    }

    public async Task<string> UploadAsync(IFormFile file, string folder, int entityId)
    {
        ValidateFile(file);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blobName = $"{folder}/{entityId}-{Guid.NewGuid():N}{extension}";
        var blobClient = _container.Value.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string? blobUrl)
    {
        if (string.IsNullOrWhiteSpace(blobUrl)) return;

        Uri uri;
        try { uri = new Uri(blobUrl); }
        catch (UriFormatException) { return; }

        if (!string.Equals(uri.Host, _container.Value.Uri.Host, StringComparison.OrdinalIgnoreCase)) return;

        var builder = new BlobUriBuilder(uri);
        await _container.Value.GetBlobClient(builder.BlobName).DeleteIfExistsAsync();
    }
}
