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


    /// M3-06: Verifies batch payment execution across a 4-attendee split bill invokes
    /// ProcessPaymentAsync exactly 4 times.

    [TestMethod]
    public async Task M3_06_ProcessBatchShares_FourAttendeeSplit_InvokesProcessorExactlyFourTimes()
    {
        // Arrange
        var splitter = new Splitter();
        var bill = new Bill(Subtotal: 100.00m, Tax: 0.00m, TipMode: TipMode.None, TipInput: 0m);
        var attendees = new List<Attendee>
        {
            new("Alex", 1, true),
            new("Blair", 1, true),
            new("Casey", 1, true),
            new("Drew", 1, true)
        };
        var shares = splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        var mockProcessor = new Mock<IPaymentProcessor>();
        mockProcessor
            .Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult(true, PaymentStatus.Completed, "txn_batch"));

        var manager = new BillPaymentManager(mockProcessor.Object);

        // Act
        var summary = await manager.ChargeBatchAsync(shares, "CAD", "cnon:card-nonce-ok");

        // Assert
        Assert.AreEqual(4, summary.SuccessCount);
        Assert.AreEqual(0, summary.FailureCount);
        mockProcessor.Verify(
            p => p.ProcessPaymentAsync(It.Is<PaymentRequest>(r => r.Amount == 25.00m)),
            Times.Exactly(4));
    }


    /// M3-07: Verifies partial batch failure resilience when attendee #2 declines while
    /// attendees #1 and #3 succeed, reporting 2 successes and 1 failure.

    [TestMethod]
    public async Task M3_07_ProcessBatchShares_SecondAttendeeDeclines_ReportsTwoSuccessesAndOneFailure()
    {
        // Arrange
        var shares = new List<Share>
        {
            new("Alex", 20.00m),
            new("Blair", 20.00m),
            new("Casey", 20.00m)
        };

        var mockProcessor = new Mock<IPaymentProcessor>();
        mockProcessor
            .SetupSequence(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult(true, PaymentStatus.Completed, "txn_01"))
            .ReturnsAsync(new PaymentResult(false, PaymentStatus.Failed, ErrorCode: "CARD_DECLINED"))
            .ReturnsAsync(new PaymentResult(true, PaymentStatus.Completed, "txn_03"));

        var manager = new BillPaymentManager(mockProcessor.Object);

        // Act
        var summary = await manager.ChargeBatchAsync(shares, "CAD", "cnon:card-nonce-ok");

        // Assert
        Assert.AreEqual(2, summary.SuccessCount);
        Assert.AreEqual(1, summary.FailureCount);
        mockProcessor.Verify(
            p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>()),
            Times.Exactly(3));
    }
}
