using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using HvacErp.Application.Common;

namespace HvacErp.Infrastructure.Services.Storage;

/// <summary>
/// Talks to Supabase Storage's REST API directly (no supabase-csharp dependency) using an
/// HttpClient pre-authorized with the service role key — see <see cref="DependencyInjection"/>.
/// </summary>
public class SupabaseStorageService : IFileStorageService
{
    private readonly HttpClient http;
    private readonly string bucket;
    private readonly string publicBaseUrl;

    public SupabaseStorageService(HttpClient http, IConfiguration configuration)
    {
        this.http = http;
        bucket = configuration["Supabase:StorageBucket"]
            ?? throw new InvalidOperationException("Missing 'Supabase:StorageBucket' configuration value.");
        var supabaseUrl = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Missing 'Supabase:Url' configuration value.");
        publicBaseUrl = $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}";
    }

    public async Task<string> UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/{bucket}/{path}");
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;
        request.Headers.Add("x-upsert", "true");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return $"{publicBaseUrl}/{path}";
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"storage/v1/object/{bucket}/{path}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> TryDeleteByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var prefix = publicBaseUrl + "/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var response = await http.DeleteAsync($"storage/v1/object/{bucket}/{url[prefix.Length..]}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
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
}
