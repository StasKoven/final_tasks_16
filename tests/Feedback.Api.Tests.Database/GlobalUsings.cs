global using Xunit;
global using Shouldly;
global using TicketSales.Api.Tests.Database;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<EventsDatabaseFixture> { }
