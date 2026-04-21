using TicketSales.Domain;
using Microsoft.EntityFrameworkCore;

namespace TicketSales.Api.Tests.Database.Events;

public class TicketCodeUniquenessTests : IClassFixture<EventsDatabaseFixture>
{
    private readonly EventsDatabaseFixture _fixture;

    public TicketCodeUniquenessTests(EventsDatabaseFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task TicketCode_UniqueConstraint_PreventsDuplicatesAtDatabaseLevelAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var ev = await db.Events.FirstAsync();
        var duplicateCode = Guid.NewGuid().ToString();

        var ticket1 = new Ticket
        {
            EventId = ev.Id,
            BuyerName = "Alice",
            BuyerEmail = "alice@example.com",
            PurchaseDate = DateTime.UtcNow,
            TicketCode = duplicateCode,
            IsUsed = false,
        };
        db.Tickets.Add(ticket1);
        await db.SaveChangesAsync();

        await using var db2 = _fixture.CreateDbContext();
        var ticket2 = new Ticket
        {
            EventId = ev.Id,
            BuyerName = "Bob",
            BuyerEmail = "bob@example.com",
            PurchaseDate = DateTime.UtcNow,
            TicketCode = duplicateCode,
            IsUsed = false,
        };
        db2.Tickets.Add(ticket2);

        var ex = await Should.ThrowAsync<Exception>(() => db2.SaveChangesAsync());
        ex.ShouldNotBeNull();
    }

    [Fact]
    public async Task TicketCode_AllSeededCodes_AreUniqueAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var totalTickets = await db.Tickets.CountAsync();
        var uniqueCodes = await db.Tickets.Select(t => t.TicketCode).Distinct().CountAsync();

        uniqueCodes.ShouldBe(totalTickets);
    }

    [Fact]
    public async Task TicketCode_DifferentTickets_DifferentEventsOk_AreUniqueAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var events = await db.Events.Take(2).ToListAsync();
        events.Count.ShouldBe(2);

        var code1 = Guid.NewGuid().ToString();
        var code2 = Guid.NewGuid().ToString();

        db.Tickets.AddRange(
            new Ticket { EventId = events[0].Id, BuyerName = "C", BuyerEmail = "c@e.com", PurchaseDate = DateTime.UtcNow, TicketCode = code1, IsUsed = false },
            new Ticket { EventId = events[1].Id, BuyerName = "D", BuyerEmail = "d@e.com", PurchaseDate = DateTime.UtcNow, TicketCode = code2, IsUsed = false }
        );

        // Should not throw — different codes
        await db.SaveChangesAsync();
    }
}
