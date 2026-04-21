namespace TicketSales.Application.Events.Responses;

public record EventResponse(
    int Id,
    string Title,
    string Description,
    string VenueName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int TotalTickets,
    int AvailableTickets,
    decimal TicketPrice);

public record EventDetailResponse(
    int Id,
    string Title,
    string Description,
    string VenueName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int TotalTickets,
    int AvailableTickets,
    decimal TicketPrice,
    int VenueId);

public record TicketResponse(
    int Id,
    int EventId,
    string BuyerName,
    string BuyerEmail,
    DateTime PurchaseDate,
    string TicketCode,
    bool IsUsed);

public record AttendeeResponse(
    string BuyerName,
    string BuyerEmail,
    string TicketCode,
    bool IsUsed);
