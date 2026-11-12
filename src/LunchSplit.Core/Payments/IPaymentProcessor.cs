namespace RestaurantBillSplitter.Core.Payments;

public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);

    Task<PaymentStatus> GetTransactionStatusAsync(string transactionId);
}
