using System.Text;
using Azure.Core;
using Azure.Storage.Blobs;

namespace Limes.Orchestrator.Storage;

/// <summary>
/// Lightweight Blob Storage helpers so the orchestrator can run either locally (file paths)
/// or as a Container Apps Job (Azure Blob URLs). Authentication uses the supplied
/// <see cref="TokenCredential"/> (managed identity in Azure, <c>az login</c> locally) — no keys.
/// </summary>
internal static class RemoteIo
{
    /// <summary>True when <paramref name="value"/> is an absolute http/https URL (i.e. a blob target).</summary>
    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Downloads the text content of a single blob addressed by its full URL.</summary>
    public static async Task<string> ReadAllTextAsync(string blobUrl, TokenCredential credential, CancellationToken ct = default)
    {
        var blob = new BlobClient(new Uri(blobUrl), credential);
        var response = await blob.DownloadContentAsync(ct);
        return response.Value.Content.ToString();
    }

    /// <summary>
    /// Uploads <paramref name="content"/> as <paramref name="blobName"/> into the blob container
    /// addressed by <paramref name="containerUrl"/>, overwriting any existing blob. Returns the blob URI.
    /// </summary>
    public static async Task<Uri> WriteAllTextAsync(
        string containerUrl, string blobName, string content, TokenCredential credential, CancellationToken ct = default)
    {
        var container = new BlobContainerClient(new Uri(containerUrl), credential);
        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, ct);
        return blob.Uri;
    }
}
