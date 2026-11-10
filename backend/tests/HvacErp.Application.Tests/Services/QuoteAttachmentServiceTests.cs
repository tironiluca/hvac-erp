using System.Text;
using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class QuoteAttachmentServiceTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02];

    private readonly AppDbContext _db = TestDb.Create();
    private readonly FakeStorage _storage = new();
    private readonly QuoteAttachmentService _sut;
    private readonly Quote _quote;

    public QuoteAttachmentServiceTests()
    {
        var client = TestDb.AddClient(_db);
        _quote = new Quote { Client = client, Number = 1 };
        _db.Quotes.Add(_quote);
        _db.SaveChanges();
        _sut = new QuoteAttachmentService(_db, _storage);
    }

    [Fact]
    public async Task A_photo_is_stored_under_the_type_its_bytes_say_whatever_the_client_claimed()
    {
        var attachment = await _sut.UploadAsync(_quote.Id, "site photo.jpg", new MemoryStream(Jpeg), "text/html");

        var (path, file) = Assert.Single(_storage.Documents);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal(Jpeg, file.Content);
        Assert.StartsWith($"quotes/{_quote.Id}/attachments/", path);
        Assert.EndsWith("-site-photo.jpg", path);
        Assert.Equal("site photo.jpg", attachment.FileName);
        Assert.Equal(path, (await _db.QuoteAttachments.SingleAsync()).FilePath); // the row keeps the path, not a link
    }

    [Fact]
    public async Task An_attachment_is_reached_through_a_link_that_expires()
    {
        var attachment = await _sut.UploadAsync(_quote.Id, "a.jpg", new MemoryStream(Jpeg), "image/jpeg");
        var path = _storage.Documents.Keys.Single();

        var url = await _sut.GetUrlAsync(_quote.Id, attachment.Id);

        Assert.Equal(FakeStorage.SignedUrl(path, DocumentLinks.Lifetime), url);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetUrlAsync(_quote.Id, attachment.Id + 1));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetUrlAsync(_quote.Id + 1, attachment.Id));
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>", "image/jpeg")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>", "image/svg+xml")]
    [InlineData("MZ this is an executable", "application/pdf")]
    [InlineData("", "image/png")]
    public async Task Anything_that_is_not_a_photo_or_a_pdf_is_refused(string content, string claimedType)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UploadAsync(_quote.Id, "innocent.jpg", new MemoryStream(Encoding.UTF8.GetBytes(content)), claimedType));

        Assert.Contains("PDF", error.Message);
        Assert.Empty(_storage.Documents);
        Assert.Empty(await _db.QuoteAttachments.ToListAsync());
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }, "image/png")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 }, "application/pdf")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63 }, "image/heic")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 }, null)] // RIFF, but a WAV
    [InlineData(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D }, null)] // ISO media, but an MP4
    public void File_types_are_recognised_by_signature(byte[] header, string? expected)
    {
        Assert.Equal(expected, UploadedFileInspector.DetectAttachmentType(header));
    }

    [Fact]
    public async Task Deleting_an_attachment_removes_its_file_from_storage_too()
    {
        var attachment = await _sut.UploadAsync(_quote.Id, "a.jpg", new MemoryStream(Jpeg), "image/jpeg");

        await _sut.DeleteAsync(_quote.Id, attachment.Id);

        Assert.Empty(await _db.QuoteAttachments.ToListAsync());
        Assert.Empty(_storage.Documents);
    }

    [Fact]
    public async Task Deleting_a_quote_removes_its_pdf_and_attachments_from_storage()
    {
        await _sut.UploadAsync(_quote.Id, "a.jpg", new MemoryStream(Jpeg), "image/jpeg");
        _quote.PdfPath = await _storage.UploadAsync($"quotes/{_quote.Id}/quote.pdf", new MemoryStream([1]), "application/pdf");
        await _storage.UploadAsync("quotes/999/other.pdf", new MemoryStream([1]), "application/pdf");
        await _db.SaveChangesAsync();
        var quotes = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db), new FakeCurrentUser(), storage: _storage);

        await quotes.DeleteAsync(_quote.Id);

        Assert.Equal(["quotes/999/other.pdf"], _storage.Documents.Keys); // only this quote's files went
    }

    [Fact]
    public async Task A_storage_failure_does_not_bring_back_a_deleted_attachment()
    {
        var attachment = await _sut.UploadAsync(_quote.Id, "a.jpg", new MemoryStream(Jpeg), "image/jpeg");
        _storage.DeleteSucceeds = false;

        await _sut.DeleteAsync(_quote.Id, attachment.Id);

        Assert.Empty(await _db.QuoteAttachments.ToListAsync());
    }
}
