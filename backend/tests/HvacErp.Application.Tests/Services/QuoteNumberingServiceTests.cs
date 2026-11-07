using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class QuoteNumberingServiceTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly QuoteNumberingService _sut;
    private readonly Product _unitA;
    private readonly Product _unitB;

    public QuoteNumberingServiceTests()
    {
        var list = TestDb.AddPriceList(_db);
        _unitA = TestDb.AddProduct(_db, list, 1000m);
        _unitB = TestDb.AddProduct(_db, list, 1200m);
        _db.SaveChanges();
        _sut = new QuoteNumberingService(_db);
    }

    private Quote Saved(Client client, int number, int version, int revision, Product external, int? parentId = null)
    {
        var quote = NewQuote(client, external);
        quote.Number = number;
        quote.Version = version;
        quote.Revision = revision;
        quote.ParentQuoteId = parentId;
        _db.Quotes.Add(quote);
        _db.SaveChanges();
        return quote;
    }

    private static Quote NewQuote(Client client, Product? external)
    {
        var quote = new Quote { ClientId = client.Id };
        if (external is not null)
        {
            quote.Products.Add(new QuoteProduct { ProductId = external.Id, Role = ProductRole.ExternalUnit });
        }
        return quote;
    }

    [Fact]
    public async Task First_quote_in_an_empty_database_is_number_1_version_1_revision_0()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var quote = NewQuote(client, _unitA);

        await _sut.AssignNumberingAsync(quote);

        Assert.Equal((1, 1, 0), (quote.Number, quote.Version, quote.Revision));
        Assert.Null(quote.ParentQuoteId);
    }

    [Fact]
    public async Task New_client_allocates_the_next_unused_number()
    {
        var other = TestDb.AddClient(_db, "CP0001", "Other");
        var client = TestDb.AddClient(_db, "CP0002", "New");
        _db.SaveChanges();
        Saved(other, 7, 1, 0, _unitA);
        Saved(other, 3, 1, 0, _unitB);

        var quote = NewQuote(client, _unitA);
        await _sut.AssignNumberingAsync(quote);

        Assert.Equal((8, 1, 0), (quote.Number, quote.Version, quote.Revision));
    }

    [Fact]
    public async Task Same_client_and_same_external_unit_bumps_the_revision()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var original = Saved(client, 5, 1, 0, _unitA);
        Saved(client, 5, 1, 1, _unitA, original.Id);

        var quote = NewQuote(client, _unitA);
        await _sut.AssignNumberingAsync(quote);

        Assert.Equal((5, 1, 2), (quote.Number, quote.Version, quote.Revision));
        Assert.Equal(original.Id, quote.ParentQuoteId);
    }

    [Fact]
    public async Task Same_client_with_a_different_external_unit_bumps_the_version()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var original = Saved(client, 5, 1, 0, _unitA);
        Saved(client, 5, 1, 1, _unitA, original.Id);

        var quote = NewQuote(client, _unitB);
        await _sut.AssignNumberingAsync(quote);

        Assert.Equal((5, 2, 0), (quote.Number, quote.Version, quote.Revision));
        Assert.NotNull(quote.ParentQuoteId);
    }

    [Fact]
    public async Task Explicit_parent_quote_is_preserved()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var first = Saved(client, 2, 1, 0, _unitA);
        var second = Saved(client, 2, 2, 0, _unitB, first.Id);

        var quote = NewQuote(client, _unitA);
        quote.ParentQuoteId = second.Id;
        await _sut.AssignNumberingAsync(quote);

        Assert.Equal(second.Id, quote.ParentQuoteId);
    }
}
