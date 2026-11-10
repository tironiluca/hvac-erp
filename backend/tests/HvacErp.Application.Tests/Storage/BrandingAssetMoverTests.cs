using Microsoft.Extensions.Configuration;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Services.Storage;

namespace HvacErp.Application.Tests.Storage;

public class BrandingAssetMoverTests
{
    private const string Legacy = "https://project.supabase.co/storage/v1/object/public/documents/";

    private readonly AppDbContext _db = TestDb.Create();
    private readonly FakeStorage _storage = new();
    private readonly BrandingAssetMover _sut;

    public BrandingAssetMoverTests()
    {
        _sut = new BrandingAssetMover(_db, _storage, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "https://project.supabase.co",
            ["Supabase:StorageBucket"] = "documents",
        }).Build());
    }

    [Fact]
    public async Task Assets_in_the_documents_bucket_are_copied_to_the_public_one_and_relinked()
    {
        await _storage.UploadAsync("branding/logo-1.png", new MemoryStream([1, 2]), "image/png");
        _db.PortalBrandings.Add(new PortalBranding
        {
            Id = 1,
            LogoUrl = Legacy + "branding/logo-1.png",            // to move
            FaviconUrl = Legacy + "branding/missing.ico",        // its file is gone
            LoginBackgroundUrl = "https://cdn.example.com/bg.jpg", // hosted elsewhere
        });
        await _db.SaveChangesAsync();

        var moves = await _sut.MoveAsync();

        var row = _db.PortalBrandings.Single();
        Assert.Equal("https://storage.example/public/branding/logo-1.png", row.LogoUrl);
        var copy = _storage.PublicAssets["branding/logo-1.png"];
        Assert.Equal("image/png", copy.ContentType);
        Assert.Equal([1, 2], copy.Content);
        Assert.Equal(Legacy + "branding/missing.ico", row.FaviconUrl);
        Assert.Equal("https://cdn.example.com/bg.jpg", row.LoginBackgroundUrl);
        Assert.Equal([("logo", true), ("favicon", false), ("login-background", false)], moves.Select(m => (m.Kind, m.Moved)));

        // A second run finds nothing left to move.
        Assert.DoesNotContain(await _sut.MoveAsync(), m => m.Moved);
    }

    [Fact]
    public async Task Nothing_happens_without_a_branding_row()
    {
        Assert.Empty(await _sut.MoveAsync());
    }
}
