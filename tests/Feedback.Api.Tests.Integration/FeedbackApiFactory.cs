using TicketSales.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace TicketSales.Api.Tests.Integration;

public class EventsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string? _externalConnectionString =
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

    private readonly PostgreSqlContainer? _postgres;

    public EventsApiFactory()
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(GetConnectionString()));
        });
    }

    public async ValueTask InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
