using AutoFixture;
using TicketSales.Application.Events.Requests;
using TicketSales.Domain;
using TicketSales.Infrastructure.Persistence;
using TicketSales.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace TicketSales.Api.Tests.Events;

public class EventServiceTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly AppDbContext _db;
    private readonly EventService _sut;
    private readonly IFixture _fixture;

    // Fixed "today" - all future events use dates after this
    private static readonly DateTimeOffset FakeNow = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    public EventServiceTests()
    {
        _timeProvider = new FakeTimeProvider(FakeNow);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new EventService(_db, _timeProvider);

        _fixture = new Fixture();
    }

    public void Dispose() => _db.Dispose();

    // ── Ticket code uniqueness ────────────────────────────────────────────────

    [Fact]
    public async Task PurchaseTickets_GeneratesUniqueTicketCodesAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 50, availableTickets: 50);

        var result = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Alice", "alice@test.com", 5));

        var codes = result.Select(t => t.TicketCode).ToList();
        codes.Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task PurchaseTickets_TicketCode_IsValidGuidFormatAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10);

        var result = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Bob", "bob@test.com", 1));

        Guid.TryParse(result[0].TicketCode, out _).ShouldBeTrue();
    }

    // ── Availability check ────────────────────────────────────────────────────

    [Fact]
    public async Task PurchaseTickets_ExactlyAvailable_SucceedsAsync()
    {
        var venue = await CreateVenueAsync(capacity: 50);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 3, availableTickets: 3);

        var result = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Carol", "carol@test.com", 3));

        result.Count.ShouldBe(3);

        var updated = await _db.Events.FindAsync(ev.Id);
        updated!.AvailableTickets.ShouldBe(0);
    }

    [Fact]
    public async Task PurchaseTickets_MoreThanAvailable_ThrowsInvalidOperationAsync()
    {
        var venue = await CreateVenueAsync(capacity: 50);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 2, availableTickets: 2);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.PurchaseTicketsAsync(ev.Id,
                new PurchaseTicketsRequest("Dave", "dave@test.com", 5)));

        ex.Message.ShouldContain("tickets available");
    }

    [Fact]
    public async Task PurchaseTickets_ZeroAvailable_ThrowsInvalidOperationAsync()
    {
        var venue = await CreateVenueAsync(capacity: 50);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 5, availableTickets: 0);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.PurchaseTicketsAsync(ev.Id,
                new PurchaseTicketsRequest("Eve", "eve@test.com", 1)));
    }

    [Fact]
    public async Task PurchaseTickets_DecreasesAvailableTicketsAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10);

        await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Frank", "frank@test.com", 3));

        var updated = await _db.Events.FindAsync(ev.Id);
        updated!.AvailableTickets.ShouldBe(7);
    }

    // ── Past event validation ─────────────────────────────────────────────────

    [Fact]
    public async Task PurchaseTickets_PastEvent_ThrowsInvalidOperationAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        // Event date is yesterday relative to FakeNow
        var pastDate = DateOnly.FromDateTime(FakeNow.UtcDateTime.AddDays(-1));
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10, date: pastDate);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.PurchaseTicketsAsync(ev.Id,
                new PurchaseTicketsRequest("Grace", "grace@test.com", 1)));

        ex.Message.ShouldContain("past events");
    }

    [Fact]
    public async Task PurchaseTickets_TodayEvent_SucceedsAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var today = DateOnly.FromDateTime(FakeNow.UtcDateTime);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10, date: today);

        var result = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Hank", "hank@test.com", 1));

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task PurchaseTickets_FutureEvent_SucceedsAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var future = DateOnly.FromDateTime(FakeNow.UtcDateTime.AddDays(30));
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10, date: future);

        var result = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Iris", "iris@test.com", 2));

        result.Count.ShouldBe(2);
    }

    // ── UseTicket ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UseTicket_ValidTicket_MarksAsUsedAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10);
        var bought = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Jack", "jack@test.com", 1));

        var code = bought[0].TicketCode;
        var result = await _sut.UseTicketAsync(code);

        result.IsUsed.ShouldBeTrue();
    }

    [Fact]
    public async Task UseTicket_AlreadyUsed_ThrowsInvalidOperationAsync()
    {
        var venue = await CreateVenueAsync(capacity: 100);
        var ev = await CreateEventAsync(venueId: venue.Id, totalTickets: 10, availableTickets: 10);
        var bought = await _sut.PurchaseTicketsAsync(ev.Id,
            new PurchaseTicketsRequest("Kate", "kate@test.com", 1));

        var code = bought[0].TicketCode;
        await _sut.UseTicketAsync(code);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UseTicketAsync(code));
        ex.Message.ShouldContain("already been used");
    }

    // ── Venue capacity ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_TotalTicketsExceedsCapacity_ThrowsInvalidOperationAsync()
    {
        var venue = await CreateVenueAsync(capacity: 50);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.CreateEventAsync(ValidCreateEventRequest(venue.Id, totalTickets: 100)));

        ex.Message.ShouldContain("capacity");
    }

    [Fact]
    public async Task CreateEvent_TotalTicketsEqualsCapacity_SucceedsAsync()
    {
        var venue = await CreateVenueAsync(capacity: 50);
        var result = await _sut.CreateEventAsync(ValidCreateEventRequest(venue.Id, totalTickets: 50));
        result.TotalTickets.ShouldBe(50);
        result.AvailableTickets.ShouldBe(50);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Venue> CreateVenueAsync(int capacity)
    {
        var venue = new Venue
        {
            Name = _fixture.Create<string>(),
            Address = _fixture.Create<string>(),
            Capacity = capacity,
        };
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();
        return venue;
    }

    private async Task<Event> CreateEventAsync(
        int venueId, int totalTickets, int availableTickets, DateOnly? date = null)
    {
        var ev = new Event
        {
            Title = _fixture.Create<string>(),
            Description = _fixture.Create<string>(),
            VenueId = venueId,
            Date = date ?? DateOnly.FromDateTime(FakeNow.UtcDateTime.AddDays(30)),
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(22, 0),
            TotalTickets = totalTickets,
            AvailableTickets = availableTickets,
            TicketPrice = 50.00m,
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    private static CreateEventRequest ValidCreateEventRequest(int venueId, int totalTickets = 10) =>
        new("Concert", "A great concert",
            venueId,
            DateOnly.FromDateTime(FakeNow.UtcDateTime.AddDays(30)),
            new TimeOnly(18, 0),
            new TimeOnly(22, 0),
            totalTickets,
            49.99m);
}
