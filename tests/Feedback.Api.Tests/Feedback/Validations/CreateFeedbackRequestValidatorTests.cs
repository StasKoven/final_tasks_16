using TicketSales.Application.Events.Requests;
using TicketSales.Application.Events.Validators;
using FluentValidation.TestHelper;

namespace TicketSales.Api.Tests.Events.Validations;

public class CreateEventRequestValidatorTests
{
    private static readonly CreateEventRequestValidator Validator = new();

    private static CreateEventRequest ValidRequest() => new(
        "Summer Concert",
        "A great outdoor concert",
        1,
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        new TimeOnly(18, 0),
        new TimeOnly(22, 0),
        100,
        49.99m);

    [Fact]
    public void ValidRequest_PassesAllRules()
    {
        var result = Validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Title_Empty_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Title = "" });
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_TooLong_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Title = new string('A', 201) });
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_AtMaxLength_NoError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Title = new string('A', 200) });
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Description_Empty_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Description = "" });
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooLong_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Description = new string('A', 2001) });
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void VenueId_Zero_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { VenueId = 0 });
        result.ShouldHaveValidationErrorFor(x => x.VenueId);
    }

    [Fact]
    public void VenueId_Negative_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { VenueId = -1 });
        result.ShouldHaveValidationErrorFor(x => x.VenueId);
    }

    [Fact]
    public void TotalTickets_Zero_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { TotalTickets = 0 });
        result.ShouldHaveValidationErrorFor(x => x.TotalTickets);
    }

    [Fact]
    public void TotalTickets_Negative_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { TotalTickets = -5 });
        result.ShouldHaveValidationErrorFor(x => x.TotalTickets);
    }

    [Fact]
    public void TicketPrice_Negative_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { TicketPrice = -1m });
        result.ShouldHaveValidationErrorFor(x => x.TicketPrice);
    }

    [Fact]
    public void TicketPrice_Zero_NoError()
    {
        var result = Validator.TestValidate(ValidRequest() with { TicketPrice = 0m });
        result.ShouldNotHaveValidationErrorFor(x => x.TicketPrice);
    }

    [Fact]
    public void EndTime_BeforeStartTime_HasError()
    {
        var start = new TimeOnly(20, 0);
        var end = new TimeOnly(18, 0);
        var result = Validator.TestValidate(ValidRequest() with { StartTime = start, EndTime = end });
        result.ShouldHaveValidationErrorFor(x => x.EndTime);
    }

    [Fact]
    public void EndTime_AfterStartTime_NoError()
    {
        var result = Validator.TestValidate(ValidRequest());
        result.ShouldNotHaveValidationErrorFor(x => x.EndTime);
    }
}

public class PurchaseTicketsRequestValidatorTests
{
    private static readonly PurchaseTicketsRequestValidator Validator = new();

    private static PurchaseTicketsRequest ValidRequest() =>
        new("Alice Smith", "alice@test.com", 2);

    [Fact]
    public void ValidRequest_PassesAllRules()
    {
        var result = Validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BuyerName_Empty_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { BuyerName = "" });
        result.ShouldHaveValidationErrorFor(x => x.BuyerName);
    }

    [Fact]
    public void BuyerEmail_Invalid_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { BuyerEmail = "not-an-email" });
        result.ShouldHaveValidationErrorFor(x => x.BuyerEmail);
    }

    [Fact]
    public void Quantity_Zero_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Quantity = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_Eleven_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Quantity = 11 });
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_Ten_NoError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Quantity = 10 });
        result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
    }
}
