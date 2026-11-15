namespace RestaurantBillSplitter.Core.Payments;


/// Domain exception thrown when an underlying payment gateway request times out or fails at the network layer.

public sealed class PaymentGatewayTimeoutException : Exception
{
    public PaymentGatewayTimeoutException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}