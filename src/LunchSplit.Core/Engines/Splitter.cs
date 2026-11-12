using RestaurantBillSplitter.Core.Helpers;
using RestaurantBillSplitter.Core.Models;

namespace RestaurantBillSplitter.Core.Engines;

public sealed class Splitter
{
    private readonly BillValidator _validator;
    private readonly Rounder _rounder;

    public Splitter() : this(new BillValidator(), new Rounder())
    {
    }

    public Splitter(BillValidator validator, Rounder rounder)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _rounder = rounder ?? throw new ArgumentNullException(nameof(rounder));
    }

    public decimal ComputeTip(decimal subtotal, TipMode mode, decimal tipInput)
    {
        if (subtotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(subtotal), "Subtotal cannot be negative.");
        }

        if (tipInput < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tipInput), "Tip input cannot be negative.");
        }

        return mode switch
        {
            TipMode.None => 0m,
            TipMode.Percent => subtotal * tipInput / 100m,
            TipMode.Fixed => tipInput,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unknown tip mode.")
        };
    }

    public List<Share> CalculateShares(Bill bill, List<Attendee> attendees, RoundingMode roundingMode)
    {
        var validation = _validator.Validate(bill, attendees);
        if (!validation.IsValid)
        {
            throw new ArgumentException(string.Join(" ", validation.Errors));
        }

        var included = attendees.Where(attendee => attendee.Included).ToList();
        if (included.Count == 0)
        {
            throw new InvalidOperationException("At least one attendee must be included.");
        }

        var totalWeight = included.Sum(attendee => attendee.Weight);
        if (totalWeight == 0)
        {
            throw new InvalidOperationException("The total included attendee weight must be greater than zero.");
        }

        var tip = ComputeTip(bill.Subtotal, bill.TipMode, bill.TipInput);
        var grandTotal = bill.Subtotal + bill.Tax + tip;

        var rawShares = attendees.Select(attendee =>
            new Share(
                attendee.Name,
                attendee.Included
                    ? grandTotal * attendee.Weight / totalWeight
                    : 0m))
            .ToList();

        return _rounder.RoundShares(rawShares, roundingMode);
    }
}
