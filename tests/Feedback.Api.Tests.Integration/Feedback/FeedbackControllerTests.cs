using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using TicketSales.Application.Events.Requests;
using TicketSales.Application.Events.Responses;
using TicketSales.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace TicketSales.Api.Tests.Integration.Events;

public class EventControllerTests : IClassFixture<EventsApiFactory>, IAsyncLifetime
{
    private readonly EventsApiFactory _factory;
    private readonly HttpClient _client;
    private readonly IFixture _fixture;

    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

    public EventControllerTests(EventsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _fixture = new Fixture();
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tickets.RemoveRange(db.Tickets);
        db.Events.RemoveRange(db.Events);
        db.Venues.RemoveRange(db.Venues);
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> CreateVenueInDbAsync(int capacity = 500)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var venue = new TicketSales.Domain.Venue
        {
            Name = _fixture.Create<string>(),
            Address = _fixture.Create<string>(),
            Capacity = capacity,
        };
        db.Venues.Add(venue);
        await db.SaveChangesAsync();
        return venue.Id;
    }

    private async Task<EventResponse> CreateEventAsync(int? venueId = null, int tickets = 50)
    {
        var vid = venueId ?? await CreateVenueInDbAsync();
        var req = new CreateEventRequest(
            _fixture.Create<string>()[..20],
            _fixture.Create<string>(),
            vid,
            FutureDate,
            new TimeOnly(18, 0),
            new TimeOnly(22, 0),
            tickets,
            49.99m);
        var response = await _client.PostAsJsonAsync("/api/events", req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    // ── GET /api/events ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUpcoming_NoEvents_ReturnsEmptyListAsync()
    {
        var response = await _client.GetAsync("/api/events");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
        items.ShouldNotBeNull();
        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUpcoming_AfterCreating_ReturnsAllAsync()
    {
        await CreateEventAsync();
        await CreateEventAsync();

        var response = await _client.GetAsync("/api/events");
        var items = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
        items!.Count.ShouldBe(2);
    }

    // ── GET /api/events/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingEvent_ReturnsEventWithAvailabilityAsync()
    {
        var created = await CreateEventAsync(tickets: 100);

        var response = await _client.GetAsync($"/api/events/{created.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        detail!.Id.ShouldBe(created.Id);
        detail.AvailableTickets.ShouldBe(100);
    }

    [Fact]
    public async Task GetById_NotExisting_Returns404Async()
    {
        var response = await _client.GetAsync("/api/events/99999");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /api/events ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201Async()
    {
        var venueId = await CreateVenueInDbAsync(200);
        var req = new CreateEventRequest(
            "Test Event", "Description", venueId,
            FutureDate, new TimeOnly(19, 0), new TimeOnly(23, 0), 100, 25m);

        var response = await _client.PostAsJsonAsync("/api/events", req);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<EventResponse>();
        result!.TotalTickets.ShouldBe(100);
        result.AvailableTickets.ShouldBe(100);
    }

    [Fact]
    public async Task Create_InvalidTitle_Returns422Async()
    {
        var venueId = await CreateVenueInDbAsync();
        var req = new CreateEventRequest(
            "", "Description", venueId,
            FutureDate, new TimeOnly(18, 0), new TimeOnly(22, 0), 10, 10m);

        var response = await _client.PostAsJsonAsync("/api/events", req);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_ExceedsVenueCapacity_Returns409Async()
    {
        var venueId = await CreateVenueInDbAsync(capacity: 50);
        var req = new CreateEventRequest(
            "Big Event", "Description", venueId,
            FutureDate, new TimeOnly(18, 0), new TimeOnly(22, 0), 100, 10m);

        var response = await _client.PostAsJsonAsync("/api/events", req);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── POST /api/events/{id}/tickets ─────────────────────────────────────────

    [Fact]
    public async Task PurchaseTickets_ValidRequest_Returns201WithTicketsAsync()
    {
        var ev = await CreateEventAsync(tickets: 20);
        var req = new PurchaseTicketsRequest("Alice Smith", "alice@test.com", 3);

        var response = await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets", req);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var tickets = await response.Content.ReadFromJsonAsync<List<TicketResponse>>();
        tickets!.Count.ShouldBe(3);
        tickets.All(t => !string.IsNullOrEmpty(t.TicketCode)).ShouldBeTrue();
    }

    [Fact]
    public async Task PurchaseTickets_MoreThanAvailable_Returns409Async()
    {
        var ev = await CreateEventAsync(tickets: 2);
        var req = new PurchaseTicketsRequest("Bob", "bob@test.com", 5);

        var response = await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets", req);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PurchaseTickets_ReducesAvailableTicketsAsync()
    {
        var ev = await CreateEventAsync(tickets: 10);
        await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets",
            new PurchaseTicketsRequest("Carol", "carol@test.com", 4));

        var detailResponse = await _client.GetAsync($"/api/events/{ev.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<EventDetailResponse>();
        detail!.AvailableTickets.ShouldBe(6);
    }

    // ── GET /api/tickets/{code} ───────────────────────────────────────────────

    [Fact]
    public async Task GetTicketByCode_ValidCode_Returns200Async()
    {
        var ev = await CreateEventAsync(tickets: 10);
        var purchaseRes = await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets",
            new PurchaseTicketsRequest("Dave", "dave@test.com", 1));
        var tickets = await purchaseRes.Content.ReadFromJsonAsync<List<TicketResponse>>();
        var code = tickets![0].TicketCode;

        var response = await _client.GetAsync($"/api/tickets/{code}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var ticket = await response.Content.ReadFromJsonAsync<TicketResponse>();
        ticket!.TicketCode.ShouldBe(code);
        ticket.IsUsed.ShouldBeFalse();
    }

    [Fact]
    public async Task GetTicketByCode_InvalidCode_Returns404Async()
    {
        var response = await _client.GetAsync("/api/tickets/nonexistent-code");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── PATCH /api/tickets/{code}/use — prevent double use ────────────────────

    [Fact]
    public async Task UseTicket_FirstUse_SucceedsAsync()
    {
        var ev = await CreateEventAsync(tickets: 10);
        var purchaseRes = await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets",
            new PurchaseTicketsRequest("Eve", "eve@test.com", 1));
        var tickets = await purchaseRes.Content.ReadFromJsonAsync<List<TicketResponse>>();
        var code = tickets![0].TicketCode;

        var response = await _client.PatchAsync($"/api/tickets/{code}/use", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var ticket = await response.Content.ReadFromJsonAsync<TicketResponse>();
        ticket!.IsUsed.ShouldBeTrue();
    }

    [Fact]
    public async Task UseTicket_SecondUse_Returns409Async()
    {
        var ev = await CreateEventAsync(tickets: 10);
        var purchaseRes = await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets",
            new PurchaseTicketsRequest("Frank", "frank@test.com", 1));
        var tickets = await purchaseRes.Content.ReadFromJsonAsync<List<TicketResponse>>();
        var code = tickets![0].TicketCode;

        await _client.PatchAsync($"/api/tickets/{code}/use", null);
        var secondUse = await _client.PatchAsync($"/api/tickets/{code}/use", null);

        secondUse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── GET /api/events/{id}/attendees ────────────────────────────────────────

    [Fact]
    public async Task GetAttendees_AfterPurchase_ReturnsAttendeesAsync()
    {
        var ev = await CreateEventAsync(tickets: 10);
        await _client.PostAsJsonAsync($"/api/events/{ev.Id}/tickets",
            new PurchaseTicketsRequest("Grace", "grace@test.com", 2));

        var response = await _client.GetAsync($"/api/events/{ev.Id}/attendees");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var attendees = await response.Content.ReadFromJsonAsync<List<AttendeeResponse>>();
        attendees!.Count.ShouldBe(2);
        attendees.All(a => a.BuyerEmail == "grace@test.com").ShouldBeTrue();
    }
}
