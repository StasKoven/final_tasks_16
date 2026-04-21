using TicketSales.Domain;
using Microsoft.EntityFrameworkCore;

namespace TicketSales.Api.Tests.Database.Events;

public class VenueCapacityTests : IClassFixture<EventsDatabaseFixture>
{
    private readonly EventsDatabaseFixture _fixture;

    public VenueCapacityTests(EventsDatabaseFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task SeededEvents_TotalTickets_NeverExceedVenueCapacityAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var violating = await db.Events
            .Include(e => e.Venue)
            .Where(e => e.TotalTickets > e.Venue.Capacity)
            .CountAsync();

        violating.ShouldBe(0);
    }

    [Fact]
    public async Task SeededData_ContainsVenuesWithEventsAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var venuesWithEvents = await db.Venues
            .Where(v => v.Events.Any())
            .CountAsync();

        venuesWithEvents.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SeededData_AllTickets_BelongToExistingEventsAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var orphanedTickets = await db.Tickets
            .Where(t => !db.Events.Any(e => e.Id == t.EventId))
            .CountAsync();

        orphanedTickets.ShouldBe(0);
    }

    [Fact]
    public async Task SeededData_TotalTicketCount_MeetsMinimumThresholdAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var totalTickets = await db.Tickets.CountAsync();
        var totalEvents = await db.Events.CountAsync();
        var totalVenues = await db.Venues.CountAsync();

        totalVenues.ShouldBeGreaterThanOrEqualTo(10);
        totalEvents.ShouldBeGreaterThanOrEqualTo(100);
        // Total records across all entities >= 10,000
        (totalTickets + totalEvents + totalVenues).ShouldBeGreaterThanOrEqualTo(10_000);
    }
}
