using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using HvacErp.Application.Common;
using HvacErp.Infrastructure.Services.Storage;

namespace HvacErp.Application.Tests.Storage;

/// <summary>The exact requests sent to Supabase Storage, against a recorded stand-in for its REST API.</summary>
public class SupabaseStorageServiceTests
{
    private readonly RecordingHandler _handler = new();
    private readonly SupabaseStorageService _sut;

    public SupabaseStorageServiceTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "https://project.supabase.co/",
            ["Supabase:StorageBucket"] = "documents",
            ["Supabase:PublicBucket"] = "public-assets",
        }).Build();
        _sut = new SupabaseStorageService(new HttpClient(_handler) { BaseAddress = new Uri("https://project.supabase.co/") }, configuration);
    }

    [Fact]
    public async Task A_document_is_uploaded_to_the_private_bucket_and_known_by_its_path()
    {
        var stored = await _sut.UploadAsync("quotes/7/quote 7.pdf", new MemoryStream([1, 2, 3]), "application/pdf");

        Assert.Equal("quotes/7/quote 7.pdf", stored); // a path, not a URL
        var request = Assert.Single(_handler.Requests);
        Assert.Equal((HttpMethod.Post, "/storage/v1/object/documents/quotes/7/quote%207.pdf"), (request.Method, request.Path));
        Assert.Equal("application/pdf", request.ContentType);
        Assert.Equal("true", request.Upsert);
    }

    [Fact]
    public async Task A_public_asset_goes_to_the_public_bucket_and_gets_a_permanent_url()
    {
        var url = await _sut.UploadPublicAsync("branding/logo.png", new MemoryStream([1]), "image/png");

        Assert.Equal("https://project.supabase.co/storage/v1/object/public/public-assets/branding/logo.png", url);
        Assert.Equal("/storage/v1/object/public-assets/branding/logo.png", Assert.Single(_handler.Requests).Path);
    }

    [Theory]
    [InlineData("""{"signedURL":"/object/sign/documents/quotes/7/q.pdf?token=abc"}""")]
    [InlineData("""{"signedUrl":"object/sign/documents/quotes/7/q.pdf?token=abc"}""")]
    public async Task A_signed_link_is_requested_for_the_lifetime_and_made_absolute(string response)
    {
        _handler.Respond(HttpStatusCode.OK, response);

        var url = await _sut.CreateSignedUrlAsync("quotes/7/q.pdf", TimeSpan.FromMinutes(10));

        Assert.Equal("https://project.supabase.co/storage/v1/object/sign/documents/quotes/7/q.pdf?token=abc", url);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal((HttpMethod.Post, "/storage/v1/object/sign/documents/quotes/7/q.pdf"), (request.Method, request.Path));
        Assert.Equal("""{"expiresIn":600}""", request.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Signing_a_file_that_is_not_there_is_not_found(HttpStatusCode status)
    {
        _handler.Respond(status, """{"error":"not_found","message":"Object not found"}""");

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateSignedUrlAsync("quotes/7/gone.pdf", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task A_storage_outage_while_signing_is_an_error_not_a_missing_file()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable, "");

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.CreateSignedUrlAsync("quotes/7/q.pdf", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task A_stored_value_that_is_already_a_link_is_returned_untouched()
    {
        var url = await _sut.CreateSignedUrlAsync("https://example.com/demo/quote.pdf", TimeSpan.FromMinutes(1));

        Assert.Equal("https://example.com/demo/quote.pdf", url);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Deleting_reports_failure_instead_of_throwing()
    {
        Assert.True(await _sut.TryDeleteAsync("quotes/7/q.pdf"));
        Assert.Equal((HttpMethod.Delete, "/storage/v1/object/documents/quotes/7/q.pdf"), (_handler.Requests[0].Method, _handler.Requests[0].Path));

        _handler.Respond(HttpStatusCode.InternalServerError, "");
        Assert.False(await _sut.TryDeleteAsync("quotes/7/q.pdf"));

        _handler.Throw = true;
        Assert.False(await _sut.TryDeleteAsync("quotes/7/q.pdf"));
    }

    [Fact]
    public async Task A_document_is_read_back_with_the_service_key_whatever_the_buckets_visibility()
    {
        _handler.Respond(HttpStatusCode.OK, "PNG", "image/png");

        var file = await _sut.TryDownloadDocumentAsync("branding/logo.png");

        Assert.Equal(("PNG", "image/png"), (Encoding.UTF8.GetString(file!.Content), file.ContentType));
        Assert.Equal((HttpMethod.Get, "/storage/v1/object/documents/branding/logo.png"), (_handler.Requests[0].Method, _handler.Requests[0].Path));

        _handler.Respond(HttpStatusCode.NotFound, "");
        Assert.Null(await _sut.TryDownloadDocumentAsync("branding/missing.png"));
    }

    private sealed record Recorded(HttpMethod Method, string Path, string? ContentType, string? Upsert, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";
        private string _contentType = "application/json";

        public List<Recorded> Requests { get; } = [];
        public bool Throw { get; set; }

        public void Respond(HttpStatusCode status, string body, string contentType = "application/json") =>
            (_status, _body, _contentType) = (status, body, contentType);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new HttpRequestException("storage unreachable");
            }

            Requests.Add(new Recorded(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.TryGetValues("x-upsert", out var upsert) ? upsert.Single() : null,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, _contentType) };
        }
    }
}
