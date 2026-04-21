using TicketSales.Domain;
using Microsoft.EntityFrameworkCore;

namespace TicketSales.Api.Tests.Database.Events;

public class AvailableTicketsAtomicityTests : IClassFixture<EventsDatabaseFixture>
{
    private readonly EventsDatabaseFixture _fixture;

    public AvailableTicketsAtomicityTests(EventsDatabaseFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task PurchaseTicket_WithTransaction_DecreasesAvailableTicketsAtomicallyAsync()
    {
        await using var db = _fixture.CreateDbContext();

        // Pick event with available tickets
        var ev = await db.Events.FirstAsync(e => e.AvailableTickets > 0);
        var originalAvailable = ev.AvailableTickets;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var ticket = new Ticket
        {
            EventId = ev.Id,
            BuyerName = "Test Buyer",
            BuyerEmail = "buyer@test.com",
            PurchaseDate = DateTime.UtcNow,
            TicketCode = Guid.NewGuid().ToString(),
            IsUsed = false,
        };
        db.Tickets.Add(ticket);
        ev.AvailableTickets--;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var db2 = _fixture.CreateDbContext();
        var updated = await db2.Events.FindAsync(ev.Id);
        updated!.AvailableTickets.ShouldBe(originalAvailable - 1);
    }

    [Fact]
    public async Task RolledBackTransaction_DoesNotChangeAvailableTicketsAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var ev = await db.Events.FirstAsync(e => e.AvailableTickets > 0);
        var originalAvailable = ev.AvailableTickets;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var ticket = new Ticket
        {
            EventId = ev.Id,
            BuyerName = "Rollback Buyer",
            BuyerEmail = "rollback@test.com",
            PurchaseDate = DateTime.UtcNow,
            TicketCode = Guid.NewGuid().ToString(),
            IsUsed = false,
        };
        db.Tickets.Add(ticket);
        ev.AvailableTickets--;
        await db.SaveChangesAsync();
        await transaction.RollbackAsync();

        await using var db2 = _fixture.CreateDbContext();
        var unchanged = await db2.Events.FindAsync(ev.Id);
        unchanged!.AvailableTickets.ShouldBe(originalAvailable);
    }

    [Fact]
    public async Task SeededEvents_AvailableTickets_NeverExceedTotalTicketsAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var violating = await db.Events
            .Where(e => e.AvailableTickets > e.TotalTickets)
            .CountAsync();

        violating.ShouldBe(0);
    }
}
