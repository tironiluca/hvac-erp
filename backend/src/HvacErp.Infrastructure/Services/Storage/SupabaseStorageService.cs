using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using HvacErp.Application.Common;

namespace HvacErp.Infrastructure.Services.Storage;

/// <summary>
/// Talks to Supabase Storage's REST API directly (no supabase-csharp dependency) using an
/// HttpClient pre-authorized with the service role key — see <see cref="DependencyInjection"/>.
/// <para>
/// Two buckets: <c>Supabase:StorageBucket</c> holds the private documents and must be a
/// <b>private</b> bucket — its objects leave only as signed links; <c>Supabase:PublicBucket</c>
/// holds the branding assets and must be a <b>public</b> one.
/// </para>
/// </summary>
public class SupabaseStorageService : IFileStorageService
{
    private readonly HttpClient http;
    private readonly string documentsBucket;
    private readonly string publicBucket;
    private readonly string storageBaseUrl;

    public SupabaseStorageService(HttpClient http, IConfiguration configuration)
    {
        this.http = http;
        documentsBucket = configuration["Supabase:StorageBucket"]
            ?? throw new InvalidOperationException("Missing 'Supabase:StorageBucket' configuration value.");
        publicBucket = configuration["Supabase:PublicBucket"]
            ?? throw new InvalidOperationException("Missing 'Supabase:PublicBucket' configuration value.");
        var supabaseUrl = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Missing 'Supabase:Url' configuration value.");
        storageBaseUrl = $"{supabaseUrl.TrimEnd('/')}/storage/v1";
    }

    /// <summary>The public URL prefix of the documents bucket as it was while that bucket was public.</summary>
    public string LegacyPublicDocumentsPrefix => $"{storageBaseUrl}/object/public/{documentsBucket}/";

    public async Task<string> UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await PutAsync(documentsBucket, path, content, contentType, cancellationToken);
        return path;
    }

    public async Task<string> UploadPublicAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await PutAsync(publicBucket, path, content, contentType, cancellationToken);
        return $"{storageBaseUrl}/object/public/{publicBucket}/{EscapePath(path)}";
    }

    public async Task<string> CreateSignedUrlAsync(string path, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        // A value that was never a path of ours (e.g. a placeholder link on demo rows) is not signable.
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            return path;
        }

        using var response = await http.PostAsJsonAsync(
            $"storage/v1/object/sign/{documentsBucket}/{EscapePath(path)}",
            new { expiresIn = (int)Math.Ceiling(lifetime.TotalSeconds) },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            // Storage answers 400 / 404 for an object that is not there (e.g. deleted by hand).
            throw new NotFoundException("Stored file", path);
        }
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var signed = (body.RootElement.TryGetProperty("signedURL", out var value) || body.RootElement.TryGetProperty("signedUrl", out value)
                ? value.GetString()
                : null)
            ?? throw new InvalidOperationException("Supabase Storage returned no signed URL.");

        // The API answers with a path relative to the storage root: /object/sign/<bucket>/<path>?token=…
        return signed.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? signed
            : $"{storageBaseUrl}/{signed.TrimStart('/')}";
    }

    public async Task<bool> TryDeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.DeleteAsync($"storage/v1/object/{documentsBucket}/{EscapePath(path)}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return false;
        }
    }

    public async Task<StoredFile?> TryDownloadDocumentAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            // The service-role key reads the object whether its bucket is public or private.
            using var response = await http.GetAsync($"storage/v1/object/{documentsBucket}/{EscapePath(path)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return new StoredFile(
                await response.Content.ReadAsByteArrayAsync(cancellationToken),
                response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return null;
        }
    }

    public async Task<byte[]?> TryDownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            // Absolute URI overrides the client's BaseAddress; the service-role auth headers are
            // harmless on a public object. Cap the read so a mis-set URL can't pull a huge file.
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 8_000_000)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task PutAsync(string bucket, string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/{bucket}/{EscapePath(path)}");
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;
        request.Headers.Add("x-upsert", "true");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Escapes each segment of an object path, keeping the slashes between them.</summary>
    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
