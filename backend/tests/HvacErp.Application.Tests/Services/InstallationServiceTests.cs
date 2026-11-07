using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class InstallationServiceTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly RecordingCalendarNotifier _notifier = new();
    private readonly InstallationService _sut;
    private readonly Client _client;
    private readonly Installer _luigi;
    private readonly Installer _mario;

    private static DateTime Tomorrow => DateTime.UtcNow.Date.AddDays(1);

    public InstallationServiceTests()
    {
        _client = TestDb.AddClient(_db);
        _luigi = new Installer { Name = "Luigi", Crew = 1 };
        _mario = new Installer { Name = "Mario", Crew = 1 };
        _db.Installers.AddRange(_luigi, _mario);
        _db.SaveChanges();
        _sut = new InstallationService(_db, new FakeCurrentUser("planner@example.com"), _notifier);
    }

    private Quote AddQuote(string status, int revision = 0)
    {
        var quote = new Quote { ClientId = _client.Id, Number = 12, Version = 3, Revision = revision, CurrentStatus = status };
        _db.Quotes.Add(quote);
        _db.SaveChanges();
        return quote;
    }

    private UpsertScheduledInstallationRequest Request(int quoteId, DateTime? date = null, params int[] installers) =>
        new(quoteId, date ?? Tomorrow, DaySlotCodes.Morning, "Bring ladder", installers.ToList());

    [Fact]
    public async Task Create_schedules_a_confirmed_quote_and_notifies_the_calendar()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed, revision: 2);

        var dto = await _sut.CreateAsync(Request(quote.Id, null, _luigi.Id, _mario.Id, _luigi.Id));

        Assert.Equal("12/3.2", dto.QuoteNumber);
        Assert.Equal("Milano", dto.City);
        Assert.Equal(2, dto.Installers.Count); // duplicate installer id de-duplicated
        Assert.Equal("planner@example.com", dto.UpdatedBy);
        Assert.Same(dto, Assert.Single(_notifier.Changed));
    }

    [Theory]
    [InlineData(QuoteStatusCodes.Draft)]
    [InlineData(QuoteStatusCodes.SurveyDone)]
    [InlineData(QuoteStatusCodes.Recalled)]
    public async Task Only_confirmed_quotes_can_be_scheduled(string status)
    {
        var quote = AddQuote(status);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Request(quote.Id)));
        Assert.Empty(_notifier.Changed);
    }

    [Fact]
    public async Task Scheduling_in_the_past_is_rejected()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Request(quote.Id, DateTime.UtcNow.Date.AddDays(-1))));
    }

    [Fact]
    public async Task A_quote_can_only_have_one_scheduled_installation()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed);
        await _sut.CreateAsync(Request(quote.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Request(quote.Id)));
    }

    [Fact]
    public async Task Reschedule_with_the_current_row_version_moves_the_card_and_rotates_the_token()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed);
        var created = await _sut.CreateAsync(Request(quote.Id, null, _luigi.Id));

        var moved = await _sut.RescheduleAsync(created.Id,
            new RescheduleInstallationRequest(Tomorrow.AddDays(2), DaySlotCodes.FullDay, [_mario.Id], created.RowVersion));

        Assert.Equal(DaySlotCodes.FullDay, moved.Slot);
        Assert.Equal("Mario", Assert.Single(moved.Installers).Name);
        Assert.NotEqual(created.RowVersion, moved.RowVersion);
        Assert.Equal(2, _notifier.Changed.Count);
    }

    [Fact]
    public async Task Reschedule_with_a_stale_row_version_is_a_conflict()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed);
        var created = await _sut.CreateAsync(Request(quote.Id));
        await _sut.RescheduleAsync(created.Id, new RescheduleInstallationRequest(Tomorrow, DaySlotCodes.Afternoon, [], created.RowVersion));

        await Assert.ThrowsAsync<ConflictException>(() => _sut.RescheduleAsync(created.Id,
            new RescheduleInstallationRequest(Tomorrow, DaySlotCodes.Morning, [], created.RowVersion)));
    }

    [Fact]
    public async Task Unknown_installer_is_not_found()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateAsync(Request(quote.Id, null, 999)));
    }

    [Fact]
    public async Task Delete_removes_and_notifies()
    {
        var quote = AddQuote(QuoteStatusCodes.Confirmed);
        var created = await _sut.CreateAsync(Request(quote.Id));

        await _sut.DeleteAsync(created.Id);

        Assert.Equal([created.Id], _notifier.Deleted);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(created.Id));
    }
}
