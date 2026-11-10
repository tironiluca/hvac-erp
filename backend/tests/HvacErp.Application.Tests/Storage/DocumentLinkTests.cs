using HvacErp.Application.Common;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Storage;

/// <summary>Generated PDFs are private files: rows keep a path, callers get a link that expires.</summary>
public class DocumentLinkTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly FakeStorage _storage = new();
    private readonly StubGenerators _generators = new();
    private readonly PdfTemplateService _templates;
    private readonly Quote _quote;

    public DocumentLinkTests()
    {
        _templates = new PdfTemplateService(_db, new FakeCurrentUser(), _generators, _generators, _generators);
        TestDb.SeedVatOptions(_db);
        _db.DaySlots.Add(new DaySlot { Code = DaySlotCodes.Morning, Label = "Morning" });
        _quote = new Quote { Client = TestDb.AddClient(_db), Number = 12, Version = 2, Revision = 1, Vat = VatCodes.Exempt, CurrentStatus = QuoteStatusCodes.Confirmed };
        _db.Quotes.Add(_quote);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Generating_a_quote_pdf_stores_a_path_and_returns_a_link_that_expires()
    {
        var sut = new QuotePdfService(_db, _generators, _templates, _storage);

        var url = await sut.GenerateAndStoreAsync(_quote.Id);

        var path = $"quotes/{_quote.Id}/quote-12-v2r1.pdf";
        Assert.Equal(path, _quote.PdfPath);
        Assert.Equal("application/pdf", _storage.Documents[path].ContentType);
        Assert.Equal(FakeStorage.SignedUrl(path, DocumentLinks.Lifetime), url);
        Assert.Equal(url, await sut.GetUrlAsync(_quote.Id));
    }

    [Fact]
    public async Task A_pdf_generated_under_an_older_file_name_is_replaced()
    {
        _quote.PdfPath = await _storage.UploadAsync($"quotes/{_quote.Id}/preventivo-12-v2r1.pdf", new MemoryStream([1]), "application/pdf");
        await _db.SaveChangesAsync();

        await new QuotePdfService(_db, _generators, _templates, _storage).GenerateAndStoreAsync(_quote.Id);

        Assert.Equal([$"quotes/{_quote.Id}/quote-12-v2r1.pdf"], _storage.Documents.Keys);
    }

    [Fact]
    public async Task There_is_no_link_before_a_pdf_exists_or_for_an_unknown_record()
    {
        var sut = new QuotePdfService(_db, _generators, _templates, _storage);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetUrlAsync(_quote.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetUrlAsync(_quote.Id + 1));
    }

    [Fact]
    public async Task A_link_is_given_only_to_someone_who_can_read_the_record()
    {
        var database = $"links-{Guid.NewGuid():N}";
        var store1 = TestDb.Create(FixedTenantContext.For(1, 1), database);
        var quote = new Quote { Client = TestDb.AddClient(store1), Number = 1 };
        store1.Quotes.Add(quote);
        await store1.SaveChangesAsync();
        quote.PdfPath = await _storage.UploadAsync("quotes/1/q.pdf", new MemoryStream([1]), "application/pdf");
        await store1.SaveChangesAsync();

        QuotePdfService As(AppDbContext db) => new(db, _generators, _templates, _storage);

        Assert.NotNull(await As(store1).GetUrlAsync(quote.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => As(TestDb.Create(FixedTenantContext.For(1, 2), database)).GetUrlAsync(quote.Id)); // another store
        await Assert.ThrowsAsync<NotFoundException>(() => As(TestDb.Create(FixedTenantContext.For(2, 3), database)).GetUrlAsync(quote.Id)); // another tenant
    }

    [Fact]
    public async Task Repair_and_installation_work_orders_follow_the_same_rule_and_go_with_their_record()
    {
        var repair = new Repair { ClientId = _quote.ClientId, Description = "Leak", Type = RepairType.Revision };
        var installation = new ScheduledInstallation { QuoteId = _quote.Id, StoreId = _quote.StoreId, Date = DateTime.UtcNow.AddDays(2), Slot = DaySlotCodes.Morning };
        _db.AddRange(repair, installation);
        await _db.SaveChangesAsync();
        var repairPdf = new RepairPdfService(_db, _generators, _templates, _storage);
        var installationPdf = new InstallationPdfService(_db, _generators, _templates, _storage);

        var repairUrl = await repairPdf.GenerateAndStoreAsync(repair.Id);
        var installationUrl = await installationPdf.GenerateAndStoreAsync(installation.Id);

        Assert.Equal($"repairs/{repair.Id}/revision-{repair.Id}.pdf", repair.PdfPath);
        Assert.Equal($"installations/{installation.Id}/installation-{installation.Id}.pdf", installation.ReportPdfPath);
        Assert.Equal(repairUrl, await repairPdf.GetUrlAsync(repair.Id));
        Assert.Equal(installationUrl, await installationPdf.GetUrlAsync(installation.Id));
        Assert.True((await new RepairService(_db, new FakeCurrentUser()).GetByIdAsync(repair.Id)).HasPdf);

        // Deleting the record deletes its file.
        await new RepairService(_db, new FakeCurrentUser(), _storage).DeleteAsync(repair.Id);
        await new InstallationService(_db, new FakeCurrentUser(), new RecordingCalendarNotifier(), _storage).DeleteAsync(installation.Id);
        Assert.Empty(_storage.Documents);
    }

    private sealed class StubGenerators : IQuoteDocumentGenerator, IRepairDocumentGenerator, IInstallationDocumentGenerator
    {
        private static readonly byte[] Pdf = "%PDF-1.7"u8.ToArray();

        public byte[] Generate(Quote quote, string vatLabel, PdfRenderContext context) => Pdf;

        public byte[] Generate(Repair repair, PdfRenderContext context) => Pdf;

        public byte[] Generate(ScheduledInstallation installation, string slotLabel, PdfRenderContext context) => Pdf;
    }
}
