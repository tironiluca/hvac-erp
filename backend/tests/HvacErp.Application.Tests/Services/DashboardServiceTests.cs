using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 15, 0, 0, TimeSpan.Zero); // a Thursday
    private readonly AppDbContext _db = TestDb.Create();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _db.RepairStatuses.AddRange(
            new RepairStatus { Code = RepairStatusCodes.New, Label = "Nuovo", SortOrder = 0 },
            new RepairStatus { Code = RepairStatusCodes.InProgress, Label = "In lavorazione", SortOrder = 3 },
            new RepairStatus { Code = RepairStatusCodes.Closed, Label = "Chiuso", SortOrder = 7, IsTerminal = true });
        _db.DaySlots.AddRange(
            new DaySlot { Code = DaySlotCodes.Morning, Label = "Mattina", SortOrder = 1 },
            new DaySlot { Code = DaySlotCodes.Afternoon, Label = "Pomeriggio", SortOrder = 2 });
        _db.SaveChanges();
        _sut = new DashboardService(_db, new FixedClock(Now));
    }

    private Quote AddQuote(Client client, string status, DateTime createdAt, int number)
    {
        var quote = new Quote { ClientId = client.Id, Number = number, CurrentStatus = status, CreatedAt = createdAt };
        _db.Quotes.Add(quote);
        return quote;
    }

    [Fact]
    public async Task Quote_pipeline_counts_each_waiting_step_and_lists_the_oldest_open_quotes()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        AddQuote(client, QuoteStatusCodes.Draft, Now.UtcDateTime.AddDays(-1), 1);
        AddQuote(client, QuoteStatusCodes.Draft, Now.UtcDateTime.AddDays(-9), 2);
        AddQuote(client, QuoteStatusCodes.SurveyDone, Now.UtcDateTime.AddDays(-3), 3);
        AddQuote(client, QuoteStatusCodes.Recalled, Now.UtcDateTime.AddDays(-4), 4);
        var confirmed = AddQuote(client, QuoteStatusCodes.Confirmed, Now.UtcDateTime.AddDays(-60), 5);
        var oldConfirmed = AddQuote(client, QuoteStatusCodes.Confirmed, Now.UtcDateTime.AddDays(-90), 6);
        _db.SaveChanges();
        _db.QuoteStatusEvents.AddRange(
            new QuoteStatusEvent { QuoteId = confirmed.Id, Status = QuoteStatusCodes.Confirmed, OccurredAt = Now.UtcDateTime.AddDays(-2) },
            new QuoteStatusEvent { QuoteId = oldConfirmed.Id, Status = QuoteStatusCodes.Confirmed, OccurredAt = Now.UtcDateTime.AddDays(-45) });
        _db.SaveChanges();

        var pipeline = (await _sut.GetAsync(null)).Quotes;

        Assert.Equal((2, 1, 1, 1), (pipeline.AwaitingSurvey, pipeline.AwaitingRecall, pipeline.AwaitingConfirmation, pipeline.ConfirmedLast30Days));
        Assert.Equal([2, 4, 3, 1], pipeline.OldestOpen.Select(q => q.Number));
    }

    [Fact]
    public async Task Open_repairs_are_grouped_by_non_terminal_status_in_lookup_order()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        _db.Repairs.AddRange(
            new Repair { ClientId = client.Id, Description = "a", Status = RepairStatusCodes.InProgress, Urgent = true },
            new Repair { ClientId = client.Id, Description = "b", Status = RepairStatusCodes.InProgress },
            new Repair { ClientId = client.Id, Description = "c", Status = RepairStatusCodes.Closed, Urgent = true });
        _db.SaveChanges();

        var dashboard = await _sut.GetAsync(null);

        Assert.Equal(
            [(RepairStatusCodes.New, 0), (RepairStatusCodes.InProgress, 2)],
            dashboard.OpenRepairsByStatus.Select(s => (s.Status, s.Count)));
        Assert.Equal(1, dashboard.UrgentOpenRepairs); // the closed urgent one does not count
    }

    [Fact]
    public async Task Week_defaults_to_the_current_monday_and_orders_by_date_then_slot()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var quote = AddQuote(client, QuoteStatusCodes.Confirmed, Now.UtcDateTime, 7);
        _db.SaveChanges();
        var monday = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        _db.ScheduledInstallations.AddRange(
            new ScheduledInstallation { QuoteId = quote.Id, Date = monday.AddDays(3), Slot = DaySlotCodes.Afternoon },
            new ScheduledInstallation { QuoteId = quote.Id, Date = monday.AddDays(3), Slot = DaySlotCodes.Morning },
            new ScheduledInstallation { QuoteId = quote.Id, Date = monday.AddDays(-1), Slot = DaySlotCodes.Morning }, // previous Sunday
            new ScheduledInstallation { QuoteId = quote.Id, Date = monday.AddDays(7), Slot = DaySlotCodes.Morning }); // next Monday
        _db.SaveChanges();

        var dashboard = await _sut.GetAsync(null);

        Assert.Equal(monday, dashboard.WeekStart);
        Assert.Equal([DaySlotCodes.Morning, DaySlotCodes.Afternoon], dashboard.WeekInstallations.Select(i => i.Slot));
        Assert.All(dashboard.WeekInstallations, i => Assert.Equal("7/1", i.QuoteNumber));
    }

    [Fact]
    public async Task Explicit_week_start_is_honoured()
    {
        var dashboard = await _sut.GetAsync(new DateTime(2026, 11, 8));

        Assert.Equal(new DateTime(2026, 11, 8), dashboard.WeekStart);
    }

    [Fact]
    public async Task Activity_merges_quote_and_repair_events_newest_first()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var quote = AddQuote(client, QuoteStatusCodes.SurveyDone, Now.UtcDateTime, 3);
        var repair = new Repair { ClientId = client.Id, Description = "x" };
        _db.Repairs.Add(repair);
        _db.SaveChanges();
        _db.QuoteStatusEvents.Add(new QuoteStatusEvent { QuoteId = quote.Id, Status = QuoteStatusCodes.SurveyDone, OccurredAt = Now.UtcDateTime.AddHours(-2), ChangedBy = "a@x" });
        _db.RepairStatusEvents.Add(new RepairStatusEvent { RepairId = repair.Id, Status = RepairStatusCodes.InProgress, OccurredAt = Now.UtcDateTime.AddHours(-1) });
        _db.SaveChanges();

        var activity = (await _sut.GetAsync(null)).RecentActivity;

        Assert.Collection(activity,
            a => Assert.Equal(("Repair", $"#{repair.Id}"), (a.Kind, a.Reference)),
            a => Assert.Equal(("Quote", "3/1", "a@x"), (a.Kind, a.Reference, a.ChangedBy)));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
