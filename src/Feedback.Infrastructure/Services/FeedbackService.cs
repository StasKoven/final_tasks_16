using TicketSales.Application.Abstractions;
using TicketSales.Application.Events.Requests;
using TicketSales.Application.Events.Responses;
using TicketSales.Domain;
using TicketSales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace TicketSales.Infrastructure.Services;

public class EventService(AppDbContext db, TimeProvider timeProvider) : IEventService
{
    public async Task<IReadOnlyList<EventResponse>> GetUpcomingEventsAsync(DateOnly? date, int? venueId)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var query = db.Events.Include(e => e.Venue).Where(e => e.Date >= today);

        if (date.HasValue)
            query = query.Where(e => e.Date == date.Value);

        if (venueId.HasValue)
            query = query.Where(e => e.VenueId == venueId.Value);

        var items = await query.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ToListAsync();
        return items.Select(ToResponse).ToList();
    }

    public async Task<EventDetailResponse> GetEventByIdAsync(int id)
    {
        var ev = await db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException($"Event {id} not found.");
        return ToDetailResponse(ev);
    }

    public async Task<EventResponse> CreateEventAsync(CreateEventRequest request)
    {
        var venue = await db.Venues.FindAsync(request.VenueId)
            ?? throw new KeyNotFoundException($"Venue {request.VenueId} not found.");

        if (request.TotalTickets > venue.Capacity)
            throw new InvalidOperationException(
                $"TotalTickets ({request.TotalTickets}) cannot exceed venue capacity ({venue.Capacity}).");

        var ev = new Event
        {
            Title = request.Title,
            Description = request.Description,
            VenueId = request.VenueId,
            Venue = venue,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            TotalTickets = request.TotalTickets,
            AvailableTickets = request.TotalTickets,
            TicketPrice = request.TicketPrice,
        };

        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ToResponse(ev);
    }

    public async Task<EventResponse> UpdateEventAsync(int id, UpdateEventRequest request)
    {
        var ev = await db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException($"Event {id} not found.");

        if (request.VenueId != ev.VenueId)
        {
            var venue = await db.Venues.FindAsync(request.VenueId)
                ?? throw new KeyNotFoundException($"Venue {request.VenueId} not found.");
            ev.VenueId = request.VenueId;
            ev.Venue = venue;
        }

        if (request.TotalTickets > ev.Venue.Capacity)
            throw new InvalidOperationException(
                $"TotalTickets ({request.TotalTickets}) cannot exceed venue capacity ({ev.Venue.Capacity}).");

        var ticketDiff = request.TotalTickets - ev.TotalTickets;
        ev.Title = request.Title;
        ev.Description = request.Description;
        ev.Date = request.Date;
        ev.StartTime = request.StartTime;
        ev.EndTime = request.EndTime;
        ev.TotalTickets = request.TotalTickets;
        ev.AvailableTickets = Math.Max(0, ev.AvailableTickets + ticketDiff);
        ev.TicketPrice = request.TicketPrice;

        await db.SaveChangesAsync();
        return ToResponse(ev);
    }

    public async Task<IReadOnlyList<TicketResponse>> PurchaseTicketsAsync(int eventId, PurchaseTicketsRequest request)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var ev = await db.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        if (ev.Date < today)
            throw new InvalidOperationException("Cannot purchase tickets for past events.");

        if (ev.AvailableTickets < request.Quantity)
            throw new InvalidOperationException(
                $"Only {ev.AvailableTickets} tickets available. Cannot purchase {request.Quantity}.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tickets = Enumerable.Range(0, request.Quantity).Select(_ => new Ticket
        {
            EventId = eventId,
            BuyerName = request.BuyerName,
            BuyerEmail = request.BuyerEmail,
            PurchaseDate = now,
            TicketCode = Guid.NewGuid().ToString(),
            IsUsed = false,
        }).ToList();

        ev.AvailableTickets -= request.Quantity;
        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync();

        return tickets.Select(ToTicketResponse).ToList();
    }

    public async Task<TicketResponse> GetTicketByCodeAsync(string code)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.TicketCode == code)
            ?? throw new KeyNotFoundException($"Ticket with code '{code}' not found.");
        return ToTicketResponse(ticket);
    }

    public async Task<TicketResponse> UseTicketAsync(string code)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.TicketCode == code)
            ?? throw new KeyNotFoundException($"Ticket with code '{code}' not found.");

        if (ticket.IsUsed)
            throw new InvalidOperationException("Ticket has already been used.");

        ticket.IsUsed = true;
        await db.SaveChangesAsync();
        return ToTicketResponse(ticket);
    }

    public async Task<IReadOnlyList<AttendeeResponse>> GetAttendeesAsync(int eventId)
    {
        var exists = await db.Events.AnyAsync(e => e.Id == eventId);
        if (!exists) throw new KeyNotFoundException($"Event {eventId} not found.");

        var tickets = await db.Tickets.Where(t => t.EventId == eventId).ToListAsync();
        return tickets.Select(t => new AttendeeResponse(t.BuyerName, t.BuyerEmail, t.TicketCode, t.IsUsed)).ToList();
    }

    private static EventResponse ToResponse(Event ev) => new(
        ev.Id, ev.Title, ev.Description, ev.Venue?.Name ?? "",
        ev.Date, ev.StartTime, ev.EndTime,
        ev.TotalTickets, ev.AvailableTickets, ev.TicketPrice);

    private static EventDetailResponse ToDetailResponse(Event ev) => new(
        ev.Id, ev.Title, ev.Description, ev.Venue?.Name ?? "",
        ev.Date, ev.StartTime, ev.EndTime,
        ev.TotalTickets, ev.AvailableTickets, ev.TicketPrice, ev.VenueId);

    private static TicketResponse ToTicketResponse(Ticket t) => new(
        t.Id, t.EventId, t.BuyerName, t.BuyerEmail, t.PurchaseDate, t.TicketCode, t.IsUsed);
}
