using Moq;
using RestaurantBillSplitter.Core.Engines;
using RestaurantBillSplitter.Core.Models;
using RestaurantBillSplitter.Core.Payments;

namespace RestaurantBillSplitter.Tests.Unit;


/// Tier 2 isolated in-memory unit tests using Moq to verify timeout translation,
/// DTO parameter forwarding, zero-dollar short-circuiting, and batch split payments.

[TestClass]
[TestCategory("Tier2")]
[TestCategory("Unit")]
public sealed class PaymentProcessorMoqTests
{

    /// M3-03: Simulates an HttpRequestException from IPaymentProcessor and asserts
    /// the payment manager translates it into PaymentGatewayTimeoutException.
  
    [TestMethod]
    public async Task M3_03_ProcessSingleShare_HttpRequestException_ThrowsPaymentGatewayTimeoutException()
    {
        // Arrange
        var mockProcessor = new Mock<IPaymentProcessor>();
        mockProcessor
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ThrowsAsync(new HttpRequestException("Simulated network timeout."));

        var manager = new BillPaymentManager(mockProcessor.Object);
        var share = new Share("Alex", 25.00m);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<PaymentGatewayTimeoutException>(
            () => manager.ChargeShareAsync(share, "CAD", "cnon:card-nonce-ok"));

        mockProcessor.Verify(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
    }

    /// M3-04: Verifies ProcessPaymentAsync is invoked with the exact dollar amount
    /// calculated for an attendee share.

    [TestMethod]
    public async Task M3_04_ProcessSingleShare_ValidShare_InvokesProcessorWithExactDollarAmount()
    {
        // Arrange
        var mockProcessor = new Mock<IPaymentProcessor>();
        mockProcessor
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult(true, PaymentStatus.Completed, "txn_m304"));

        var manager = new BillPaymentManager(mockProcessor.Object);
        var share = new Share("Alex", 47.85m);

        // Act
        var result = await manager.ChargeShareAsync(share, "CAD", "cnon:card-nonce-ok");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        mockProcessor.Verify(
            p => p.ProcessPaymentAsync(It.Is<PaymentRequest>(req =>
                req.Amount == 47.85m &&
                req.CurrencyCode == "CAD" &&
                req.CardNonce == "cnon:card-nonce-ok" &&
                !string.IsNullOrWhiteSpace(req.IdempotencyKey))),
            Times.Once);
    }

    /// M3-05: Verifies a $0.00 share short-circuits before hitting the payment gateway
    /// so IPaymentProcessor.ProcessPaymentAsync is never invoked.

    [TestMethod]
    public async Task M3_05_ProcessSingleShare_ZeroDollarAmount_NeverInvokesPaymentProcessor()
    {
        // Arrange
        var mockProcessor = new Mock<IPaymentProcessor>(MockBehavior.Strict);
        var manager = new BillPaymentManager(mockProcessor.Object);
        var zeroShare = new Share("ExcludedGuest", 0.00m);

        // Act
        var result = await manager.ChargeShareAsync(zeroShare, "CAD", "cnon:card-nonce-ok");

        // Assert
        Assert.IsTrue(result.IsSuccess);
        mockProcessor.Verify(
            p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()),
            Times.Never);
    }
