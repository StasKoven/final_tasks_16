# Ticket Sales Platform - Task 16

Event management and ticket sales platform with comprehensive testing suite.

## 📋 Domain

Platform for managing events and selling tickets with venue management.

### Entities

- **Event**: Id, Title, Description, Venue, Date, StartTime, EndTime, TotalTickets, AvailableTickets, TicketPrice
- **Ticket**: Id, EventId, BuyerName, BuyerEmail, PurchaseDate, TicketCode, IsUsed
- **Venue**: Id, Name, Address, Capacity

## 🚀 API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/events` | Get future events (filter by date, venue) |
| POST | `/api/events` | Create event |
| GET | `/api/events/{id}` | Get event details with availability |
| PUT | `/api/events/{id}` | Update event |
| POST | `/api/events/{id}/tickets` | Purchase ticket(s) |
| GET | `/api/tickets/{code}` | Check ticket by code |
| PATCH | `/api/tickets/{code}/use` | Mark ticket as used (entrance scanning) |
| GET | `/api/events/{id}/attendees` | Get list of ticket owners |

## 💼 Business Rules

- Cannot purchase more tickets than available
- TicketCode must be unique (auto-generated UUID)
- Event TotalTickets cannot exceed venue Capacity
- Cannot purchase tickets for past events
- Ticket can be used only once

## 🧪 Testing

### Test Coverage

- **Unit Tests**: Ticket code generation, availability validation, past event validation
- **Integration Tests** (WebApplicationFactory): Purchase flow, ticket validation, double-use prevention
- **Database Tests** (Testcontainers): Atomic availability decrement, ticket code uniqueness, venue capacity constraints
- **Performance Tests** (k6): Load testing event listings, stress testing concurrent ticket purchases (flash sale scenario)

### Running Tests

```powershell
# Unit tests
dotnet test tests/Feedback.Api.Tests/Feedback.Api.Tests.csproj

# Integration tests
dotnet test tests/Feedback.Api.Tests.Integration/Feedback.Api.Tests.Integration.csproj

# Database tests (requires Docker)
dotnet test tests/Feedback.Api.Tests.Database/Feedback.Api.Tests.Database.csproj

# Performance tests
cd tests/Feedback.Api.Tests.Performance
npm install
npm run test:smoke
npm run test:load
npm run test:stress
```

## 🗄️ Database

Uses PostgreSQL as the database. For Testcontainers, uses `Testcontainers.PostgreSql` package.

### Test Data Seeding

For performance and integration tests, the database is pre-populated with at least 10,000 records distributed across all entities using AutoFixture/Bogus for realistic test data.

## 🏗️ Project Structure

```
src/
├── Feedback.Api/            # Web API layer
├── Feedback.Application/    # Application services and business logic
├── Feedback.Domain/         # Domain entities
└── Feedback.Infrastructure/ # Data access and external services

tests/
├── Feedback.Api.Tests/                 # Unit tests
├── Feedback.Api.Tests.Integration/     # Integration tests
├── Feedback.Api.Tests.Database/        # Database tests
└── Feedback.Api.Tests.Performance/     # k6 performance tests
```

## 🛠️ Technology Stack

- **.NET 10.0**
- **PostgreSQL 16**
- **Entity Framework Core**
- **FluentValidation**
- **xUnit**
- **Testcontainers**
- **k6** (performance testing)
- **Docker**

## 🚀 Getting Started

### Prerequisites

- .NET 10.0 SDK
- Docker Desktop
- Node.js (for k6 tests)

### Running the Application

#### Using Docker Compose

```powershell
docker-compose up
```

The API will be available at `http://localhost:5000`

#### Using .NET CLI

```powershell
# Set up database connection string in appsettings.json
dotnet run --project src/Feedback.Api/Feedback.Api.csproj
```

### API Documentation

Once running, visit `http://localhost:5000/swagger` for interactive API documentation.

## 📊 CI/CD

The project includes GitHub Actions workflows:

- **CI Pipeline** (`ci.yml`): Runs on every push/PR to main branch
  - Builds solution
  - Runs unit tests
  - Runs integration tests
  - Runs database tests
  
- **Performance Tests** (`k6.yml`): Can be triggered manually or on PR changes
  - Runs k6 performance tests
  
- **Branch Naming** (`branch-name.yml`): Enforces branch naming convention
  - Requires branches to follow `feature/*` or `fix/*` pattern

## 📝 Notes

- The project uses `TicketSales` namespaces internally
- AutoFixture is used for generating non-critical test data
- Critical business rule fields are set explicitly in tests
- All timestamps use `TimeProvider` abstraction for testability

## 🔧 Configuration

Key configuration in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ticketsales;Username=postgres;Password=postgres"
  }
}
```

## 📄 License

This project is part of a learning assignment.
