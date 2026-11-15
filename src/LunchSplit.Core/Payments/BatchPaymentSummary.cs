namespace RestaurantBillSplitter.Core.Payments;

/// Summarizes the outcome of processing a batch of attendee share payments.

public sealed record BatchPaymentSummary(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<PaymentResult> Results);