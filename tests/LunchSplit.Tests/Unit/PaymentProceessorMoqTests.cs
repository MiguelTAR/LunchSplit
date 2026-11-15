using Moq;
using RestaurantBillSplitter.Core.Engines;
using RestaurantBillSplitter.Core.Models;
using RestaurantBillSplitter.Core.Payments;

namespace RestaurantBillSplitter.Tests.Unit;

/// <summary>
/// Tier 2 isolated in-memory unit tests using Moq to verify timeout translation,
/// DTO parameter forwarding, zero-dollar short-circuiting, and batch split payments.
/// </summary>
[TestClass]
[TestCategory("Tier2")]
[TestCategory("Unit")]
public sealed class PaymentProcessorMoqTests
{
    /// <summary>
    /// M3-03: Simulates an HttpRequestException from IPaymentProcessor and asserts
    /// the payment manager translates it into PaymentGatewayTimeoutException.
    /// </summary>
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