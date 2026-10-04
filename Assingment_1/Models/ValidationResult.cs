namespace RestaurantBillSplitter.Core.Models;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, Array.Empty<string>());

    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToArray());
}
