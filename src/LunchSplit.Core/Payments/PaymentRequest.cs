namespace RestaurantBillSplitter.Core.Payments;

public sealed record PaymentRequest(
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    string CardNonce,
    string? LocationId = null);
