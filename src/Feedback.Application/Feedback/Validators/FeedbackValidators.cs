using FluentValidation;
using TicketSales.Application.Events.Requests;

namespace TicketSales.Application.Events.Validators;

public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.VenueId)
            .GreaterThan(0).WithMessage("VenueId must be a valid identifier.");

        RuleFor(x => x.TotalTickets)
            .GreaterThan(0).WithMessage("TotalTickets must be greater than 0.");

        RuleFor(x => x.TicketPrice)
            .GreaterThanOrEqualTo(0).WithMessage("TicketPrice cannot be negative.");

        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime).WithMessage("EndTime must be after StartTime.");
    }
}

public class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.VenueId)
            .GreaterThan(0).WithMessage("VenueId must be a valid identifier.");

        RuleFor(x => x.TotalTickets)
            .GreaterThan(0).WithMessage("TotalTickets must be greater than 0.");

        RuleFor(x => x.TicketPrice)
            .GreaterThanOrEqualTo(0).WithMessage("TicketPrice cannot be negative.");

        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime).WithMessage("EndTime must be after StartTime.");
    }
}

public class PurchaseTicketsRequestValidator : AbstractValidator<PurchaseTicketsRequest>
{
    public PurchaseTicketsRequestValidator()
    {
        RuleFor(x => x.BuyerName)
            .NotEmpty().WithMessage("Buyer name is required.")
            .MaximumLength(100).WithMessage("Buyer name must not exceed 100 characters.");

        RuleFor(x => x.BuyerEmail)
            .NotEmpty().WithMessage("Buyer email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be at least 1.")
            .LessThanOrEqualTo(10).WithMessage("Cannot purchase more than 10 tickets at once.");
    }
}
