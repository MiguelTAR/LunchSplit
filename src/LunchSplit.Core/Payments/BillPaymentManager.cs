
using RestaurantBillSplitter.Core.Models;
// Bill Payment Manager orchestrates single and batch share payments against an IPaymentProcessor,
// enforcing zero-dollar short-circuiting, timeout exception translation, and partial-failure resilience.
namespace RestaurantBillSplitter.Core.Payments;

/// Coordinates single and batch share payments against an <see cref="IPaymentProcessor"/>,
/// enforcing zero-dollar short-circuiting, timeout exception translation, and partial-failure resilience.

public sealed class BillPaymentManager
{
    private readonly IPaymentProcessor _paymentProcessor;

    public BillPaymentManager(IPaymentProcessor paymentProcessor)
    {
        _paymentProcessor = paymentProcessor ?? throw new ArgumentNullException(nameof(paymentProcessor));
    }


    /// Charges a single attendee share. Short-circuits $0.00 shares without invoking the gateway,
    /// and translates <see cref="HttpRequestException"/> into <see cref="PaymentGatewayTimeoutException"/>.
   public async Task<PaymentResult> ChargeShareAsync(Share share, string currencyCode, string cardNonce)
    {
        ArgumentNullException.ThrowIfNull(share);

        if (share.Amount <= 0m)
        {
            return new PaymentResult(IsSuccess: true, Status: PaymentStatus.Completed);
        }

        var request = new PaymentRequest(
            Amount: share.Amount,
            CurrencyCode: currencyCode,
            IdempotencyKey: Guid.NewGuid().ToString(),
            CardNonce: cardNonce);

        try
        {
            return await _paymentProcessor.ProcessPaymentAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentGatewayTimeoutException("Payment gateway request timed out.", ex);
        }
    }

    /// Iterates across all attendee shares and processes each payment independently so a single
    /// card decline does not abort remaining attendees.

    public async Task<BatchPaymentSummary> ChargeBatchAsync(
        IEnumerable<Share> shares,
        string currencyCode,
        string cardNonce)
    {
        // Guard clause for null shares collection
        ArgumentNullException.ThrowIfNull(shares);

        var results = new List<PaymentResult>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var share in shares)
        {
            var result = await ChargeShareAsync(share, currencyCode, cardNonce);
            results.Add(result);

            if (result.IsSuccess)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }
        // Return a summary of the batch payment results
        return new BatchPaymentSummary(successCount, failureCount, results);
    }
}