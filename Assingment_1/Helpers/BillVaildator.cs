using RestaurantBillSplitter.Core.Models;

namespace RestaurantBillSplitter.Core.Helpers;

public sealed class BillValidator
{
    public ValidationResult Validate(Bill bill, List<Attendee> attendees)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(attendees);

        var errors = new List<string>();

        if (bill.Subtotal < 0)
        {
            errors.Add("Subtotal cannot be negative.");
        }

        if (bill.Tax < 0)
        {
            errors.Add("Tax cannot be negative.");
        }

        if (attendees.Count == 0)
        {
            errors.Add("At least one attendee is required.");
        }

        if (attendees.Any(attendee => attendee.Weight < 0))
        {
            errors.Add("Attendee weights cannot be negative.");
        }

        if (attendees.Any(attendee => string.IsNullOrWhiteSpace(attendee.Name)))
        {
            errors.Add("Every attendee must have a name.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
