namespace RestaurantBillSplitter.Core.Payments;

public sealed record PaymentResult(
    bool IsSuccess,
    PaymentStatus Status,
    string? TransactionId = null,
    string? ErrorCode = null);
