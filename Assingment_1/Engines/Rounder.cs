using RestaurantBillSplitter.Core.Models;

namespace RestaurantBillSplitter.Core.Engines;

public sealed class Rounder
{
    public List<Share> RoundShares(List<Share> rawShares, RoundingMode mode)
    {
        ArgumentNullException.ThrowIfNull(rawShares);

        if (rawShares.Count == 0 || mode == RoundingMode.None)
        {
            return rawShares.Select(share => new Share(share.Name, share.Amount)).ToList();
        }

        var rounded = rawShares
            .Select(share => new Share(share.Name, RoundAmount(share.Amount, mode)))
            .ToList();

        var rawTotal = rawShares.Sum(share => share.Amount);
        var targetTotal = decimal.Round(rawTotal, 2, MidpointRounding.AwayFromZero);

   
        // This preserves explicit Up/Down behavior for standalone fractional-cent inputs,
        // while ensuring calculated bill allocations add back to the exact grand total.
        if (rawTotal == targetTotal)
        {
            var roundedTotal = rounded.Sum(share => share.Amount);
            var differenceInCents = (int)((targetTotal - roundedTotal) * 100m);
            ReconcilePennies(rounded, differenceInCents);
        }
        return rounded;
    }

    private static decimal RoundAmount(decimal amount, RoundingMode mode) => mode switch
    {
        RoundingMode.Bankers => decimal.Round(amount, 2, MidpointRounding.ToEven),
        RoundingMode.Up => decimal.Ceiling(amount * 100m) / 100m,
        RoundingMode.Down => decimal.Floor(amount * 100m) / 100m,
        _ => amount
    };

    private static void ReconcilePennies(List<Share> shares, int differenceInCents)
    {
        if (shares.Count == 0 || differenceInCents == 0)
        {
            return;
        }

        var direction = Math.Sign(differenceInCents);
        var pennies = Math.Abs(differenceInCents);

        for (var index = 0; index < pennies; index++)
        {
            var shareIndex = index % shares.Count;
            var current = shares[shareIndex];
            shares[shareIndex] = current with { Amount = current.Amount + (direction * 0.01m) };
        }
    }
}
