using Bogus;
using TicketSales.Domain;
using TicketSales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace TicketSales.Api.Tests.Database;

public class EventsDatabaseFixture : IAsyncLifetime
{
    private readonly string? _externalConnectionString =
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

    private readonly PostgreSqlContainer? _postgres;

    public EventsDatabaseFixture()
    {
        if (_externalConnectionString is null)
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .Build();
        }
    }

    private string GetConnectionString() =>
        _externalConnectionString ?? _postgres!.GetConnectionString();

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();

        using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        // Clear existing data to guarantee clean state (e.g. after integration tests)
        db.Tickets.RemoveRange(db.Tickets);
        db.Events.RemoveRange(db.Events);
        db.Venues.RemoveRange(db.Venues);
        await db.SaveChangesAsync();

        await SeedDataAsync(db);
    }

    private static async Task SeedDataAsync(AppDbContext db)
    {
        var faker = new Faker();
        var random = new Random(42);

        // Create 20 venues
        const int venueCount = 20;
        var venues = new List<Venue>(venueCount);
        for (var i = 0; i < venueCount; i++)
        {
            venues.Add(new Venue
            {
                Name = faker.Company.CompanyName(),
                Address = faker.Address.FullAddress(),
                Capacity = random.Next(100, 10_000),
            });
        }
        db.Venues.AddRange(venues);
        await db.SaveChangesAsync();

        // Create 500 events spread across venues
        const int eventCount = 500;
        const int eventBatchSize = 100;
        var allEvents = new List<Event>(eventCount);

        for (var i = 0; i < eventCount; i++)
        {
            var venue = venues[i % venueCount];
            var capacity = Math.Min(venue.Capacity, random.Next(50, 500));
            var sold = random.Next(0, capacity);
            var eventDate = DateOnly.FromDateTime(faker.Date.Between(
                DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(180)));

            allEvents.Add(new Event
            {
                Title = faker.Lorem.Sentence(3),
                Description = faker.Lorem.Paragraph(),
                VenueId = venue.Id,
                Date = eventDate,
                StartTime = new TimeOnly(faker.Random.Int(10, 20), 0),
                EndTime = new TimeOnly(faker.Random.Int(21, 23), 0),
                TotalTickets = capacity,
                AvailableTickets = capacity - sold,
                TicketPrice = faker.Random.Decimal(5, 500),
            });
        }

        for (var batch = 0; batch < eventCount; batch += eventBatchSize)
        {
            var chunk = allEvents.Skip(batch).Take(eventBatchSize).ToList();
            db.Events.AddRange(chunk);
            await db.SaveChangesAsync();
        }

        // Create tickets for sold seats (~9500 tickets total)
        var tickets = new List<Ticket>();
        var usedCodes = new HashSet<string>();

        foreach (var ev in allEvents)
        {
            var sold = ev.TotalTickets - ev.AvailableTickets;
            for (var t = 0; t < sold; t++)
            {
                string code;
                do { code = Guid.NewGuid().ToString(); } while (!usedCodes.Add(code));

                tickets.Add(new Ticket
                {
                    EventId = ev.Id,
                    BuyerName = faker.Name.FullName(),
                    BuyerEmail = faker.Internet.Email(),
                    PurchaseDate = faker.Date.Past(1).ToUniversalTime(),
                    TicketCode = code,
                    IsUsed = faker.Random.Bool(),
                });
            }
        }

        const int ticketBatchSize = 500;
        for (var batch = 0; batch < tickets.Count; batch += ticketBatchSize)
        {
            var chunk = tickets.Skip(batch).Take(ticketBatchSize).ToList();
            db.Tickets.AddRange(chunk);
            await db.SaveChangesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}
