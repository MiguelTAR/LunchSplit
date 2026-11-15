using RestaurantBillSplitter.Core.Payments;
using RestaurantBillSplitter.Infrastructure.Payments;

namespace RestaurantBillSplitter.Tests.Integration;

/// <summary>
/// Tier 1 live integration tests verifying SquarePaymentService against the Square Sandbox API
/// using official Magic Test Nonces and environment-injected credentials.
/// </summary>
[TestClass]
[TestCategory("Tier1")]
[TestCategory("Integration")]
public sealed class SquareSandboxTests
{
    private SquarePaymentService _paymentService = null!;

    /// <summary>
    /// Initializes the SquarePaymentService using externalized Sandbox environment variables.
    /// </summary>
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

    /// <summary>
    /// M3-01: Verifies live Square Sandbox payment authorization succeeds using Magic Nonce cnon:card-nonce-ok.
    /// </summary>
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


