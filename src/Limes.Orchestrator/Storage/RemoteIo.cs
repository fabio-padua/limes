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
    // Azure Storage blob host suffixes across clouds. We only treat URLs on these hosts as blob
    // targets so AAD bearer tokens are never sent to an arbitrary host the user might supply.
    private static readonly string[] BlobHostSuffixes =
    [
        ".blob.core.windows.net",       // Azure public cloud
        ".blob.core.usgovcloudapi.net", // Azure US Government
        ".blob.core.chinacloudapi.cn",  // Azure China
        ".blob.core.cloudapi.de",       // Azure Germany (legacy)
    ];

    /// <summary>
    /// True only when <paramref name="value"/> is an absolute HTTPS URL on a known Azure Storage
    /// blob endpoint. Non-Azure or non-HTTPS URLs are treated as local paths, so the credential
    /// is never attached to a request bound for an untrusted host.
    /// </summary>
    public static bool IsAzureBlobUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && BlobHostSuffixes.Any(suffix => uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Downloads the text content of a single blob addressed by its full URL.</summary>
    public static async Task<string> ReadAllTextAsync(string blobUrl, TokenCredential credential, CancellationToken ct = default)
    {
        var blob = new BlobClient(new Uri(blobUrl), credential);
        var response = await blob.DownloadContentAsync(ct);
        return response.Value.Content.ToString();
    }

    /// <summary>
    /// Uploads <paramref name="content"/> as <paramref name="blobName"/> under the output location
    /// addressed by <paramref name="outputUrl"/>, overwriting any existing blob. The output URL is a
    /// container URL, optionally with a path prefix (e.g. <c>.../reports/2026/q2</c>); the prefix is
    /// preserved and <paramref name="blobName"/> is appended beneath it. Returns the blob URI.
    /// </summary>
    public static async Task<Uri> WriteAllTextAsync(
        string outputUrl, string blobName, string content, TokenCredential credential, CancellationToken ct = default)
    {
        var (containerUri, prefix) = SplitContainerAndPrefix(new Uri(outputUrl));
        var container = new BlobContainerClient(containerUri, credential);
        var fullName = string.IsNullOrEmpty(prefix) ? blobName : $"{prefix}/{blobName}";
        var blob = container.GetBlobClient(fullName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blob.UploadAsync(stream, overwrite: true, ct);
        return blob.Uri;
    }

    /// <summary>
    /// Splits a blob/container URL into the container URL (account + first path segment) and an
    /// optional blob-name prefix (the remaining path segments). This lets callers point output at
    /// either a bare container (<c>.../reports</c>) or a "folder" within it (<c>.../reports/run-1</c>)
    /// without producing a surprising upload URI.
    /// </summary>
    private static (Uri ContainerUri, string Prefix) SplitContainerAndPrefix(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var container = segments.Length > 0 ? segments[0] : string.Empty;
        var prefix = string.Join('/', segments.Skip(1));
        var containerUri = new UriBuilder(uri) { Path = "/" + container, Query = string.Empty }.Uri;
        return (containerUri, prefix);
    }
}
