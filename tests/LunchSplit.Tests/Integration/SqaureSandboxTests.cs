using RestaurantBillSplitter.Core.Payments;
using RestaurantBillSplitter.Infrastructure.Payments;

namespace RestaurantBillSplitter.Tests.Integration;


/// Tier 1 live integration tests verifying SquarePaymentService against the Square Sandbox API
/// using official Magic Test Nonces and environment-injected credentials.

[TestClass]
[TestCategory("Tier1")]
[TestCategory("Integration")]
public sealed class SquareSandboxTests
{
    private SquarePaymentService _paymentService = null!;

    /// Initializes the SquarePaymentService using externalized Sandbox environment variables.

    [TestInitialize]
    public void Setup()
    {
        var token = Environment.GetEnvironmentVariable("SQUARE_SANDBOX_ACCESS_TOKEN");
        var locationId = Environment.GetEnvironmentVariable("SQUARE_SANDBOX_LOCATION_ID");

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(locationId))
        {
            Assert.Inconclusive(
                "Square Sandbox credentials are missing. Configure SQUARE_SANDBOX_ACCESS_TOKEN and SQUARE_SANDBOX_LOCATION_ID.");
        }

        _paymentService = new SquarePaymentService(token, locationId, TimeSpan.FromSeconds(5));
    }

    /// M3-01: Verifies live Square Sandbox payment authorization succeeds using Magic Nonce cnon:card-nonce-ok.
    
    [TestMethod]
    public async Task M3_01_ProcessPaymentAsync_ValidMagicNonce_ReturnsSuccessAndTransactionId()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount: 25.50m,
            CurrencyCode: "CAD",
            IdempotencyKey: Guid.NewGuid().ToString(),
            CardNonce: "cnon:card-nonce-ok");

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(PaymentStatus.Completed, result.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TransactionId));
    }


    
    /// M3-02: Verifies live Square Sandbox card decline handling using Magic Nonce cnon:card-nonce-declined.
  
    [TestMethod]
    public async Task M3_02_ProcessPaymentAsync_DeclinedMagicNonce_ReturnsFailureWithCardDeclinedCode()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount: 15.00m,
            CurrencyCode: "CAD",
            IdempotencyKey: Guid.NewGuid().ToString(),
            CardNonce: "cnon:card-nonce-declined");

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("CARD_DECLINED", result.ErrorCode);
    }


    /// M3-08: Verifies live Square Sandbox rejects unsupported currency codes (EUR on a CAD location)
    /// with an HTTP 400 validation failure.

    [TestMethod]
    public async Task M3_08_ProcessPaymentAsync_UnsupportedCurrency_ReturnsFailureWithValidationError()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount: 20.00m,
            CurrencyCode: "EUR",
            IdempotencyKey: Guid.NewGuid().ToString(),
            CardNonce: "cnon:card-nonce-ok");

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorCode));
    }


    /// M3-09: Verifies Square Sandbox idempotency enforcement by submitting the same idempotency key twice
    /// and confirming the original TransactionId is returned without creating a duplicate charge.
 
    [TestMethod]
    public async Task M3_09_ProcessPaymentAsync_DuplicateIdempotencyKey_ReturnsOriginalTransactionId()
    {
        // Arrange
        var sharedIdempotencyKey = Guid.NewGuid().ToString();
        var request = new PaymentRequest(
            Amount: 18.75m,
            CurrencyCode: "CAD",
            IdempotencyKey: sharedIdempotencyKey,
            CardNonce: "cnon:card-nonce-ok");

        // Act
        var firstResult = await _paymentService.ProcessPaymentAsync(request);
        var secondResult = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.IsTrue(firstResult.IsSuccess);
        Assert.IsTrue(secondResult.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstResult.TransactionId));
        Assert.AreEqual(firstResult.TransactionId, secondResult.TransactionId);
    }

    /// M3-10: Verifies live transaction status retrieval via GET /v2/payments/{payment_id} returns Completed.
   
    [TestMethod]
    public async Task M3_10_GetTransactionStatusAsync_CompletedPayment_ReturnsCompletedStatus()
    {
        // Arrange: Create an approved payment first to obtain a live Sandbox TransactionId
        var request = new PaymentRequest(
            Amount: 32.00m,
            CurrencyCode: "CAD",
            IdempotencyKey: Guid.NewGuid().ToString(),
            CardNonce: "cnon:card-nonce-ok");

        var paymentResult = await _paymentService.ProcessPaymentAsync(request);
        Assert.IsTrue(paymentResult.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(paymentResult.TransactionId));

        // Act
        var status = await _paymentService.GetTransactionStatusAsync(paymentResult.TransactionId!);

        // Assert
        Assert.AreEqual(PaymentStatus.Completed, status);
    }
}
