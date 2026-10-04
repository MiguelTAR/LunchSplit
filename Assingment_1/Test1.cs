using RestaurantBillSplitter.Core.Engines;
using RestaurantBillSplitter.Core.Helpers;
using RestaurantBillSplitter.Core.Models;
using RestaurantBillSplitter.Core.Output;

namespace RestaurantBillSplitter.Tests;

[TestClass]
public sealed class BillSplitterTests
{
    [TestMethod]
    public void ComputeTip_NoTipMode_ReturnsZero()
    {
        var splitter = new Splitter();

        var result = splitter.ComputeTip(100m, TipMode.None, 50m);

        Assert.AreEqual(0.00m, result);
    }

    [TestMethod]
    public void ComputeTip_PercentTipMode_ReturnsCorrectPercent()
    {
        var splitter = new Splitter();

        var result = splitter.ComputeTip(120m, TipMode.Percent, 15m);

        Assert.AreEqual(18.00m, result);
    }

    [TestMethod]
    public void ComputeTip_FixedTipMode_ReturnsFixedAmount()
    {
        var splitter = new Splitter();

        var result = splitter.ComputeTip(100m, TipMode.Fixed, 15m);

        Assert.AreEqual(15.00m, result);
    }

    [TestMethod]
    public void ComputeTip_NegativeSubtotal_ThrowsException()
    {
        var splitter = new Splitter();

        void Act() => splitter.ComputeTip(-0.01m, TipMode.None, 0m);

        Assert.ThrowsException<ArgumentOutOfRangeException>(Act);
    }

    [TestMethod]
    public void ComputeTip_NegativeFixedTip_ThrowsException()
    {
        var splitter = new Splitter();

        void Act() => splitter.ComputeTip(100m, TipMode.Fixed, -0.01m);

        Assert.ThrowsException<ArgumentOutOfRangeException>(Act);
    }

    [TestMethod]
    public void RoundShares_NoRounding_PreservesRawDecimals()
    {
        var rounder = new Rounder();
        var raw = new List<Share> { new("A", 10.3245m), new("B", 5.6789m) };

        var result = rounder.RoundShares(raw, RoundingMode.None);

        CollectionAssert.AreEqual(new[] { 10.3245m, 5.6789m }, result.Select(x => x.Amount).ToArray());
    }

    [TestMethod]
    public void RoundShares_BankersRounding_RoundsToNearestEven()
    {
        var rounder = new Rounder();
        var raw = new List<Share> { new("A", 10.325m), new("B", 10.335m) };

        var result = rounder.RoundShares(raw, RoundingMode.Bankers);

        CollectionAssert.AreEqual(new[] { 10.32m, 10.34m }, result.Select(x => x.Amount).ToArray());
    }

    [TestMethod]
    public void RoundShares_RoundUp_AppliesCeilingToCents()
    {
        var rounder = new Rounder();
        var raw = new List<Share> { new("A", 10.331m) };

        var result = rounder.RoundShares(raw, RoundingMode.Up);

        Assert.AreEqual(10.34m, result.Single().Amount);
    }

    [TestMethod]
    public void RoundShares_RoundDown_AppliesFloorToCents()
    {
        var rounder = new Rounder();
        var raw = new List<Share> { new("A", 10.339m) };

        var result = rounder.RoundShares(raw, RoundingMode.Down);

        Assert.AreEqual(10.33m, result.Single().Amount);
    }

    [TestMethod]
    public void RoundShares_UnevenSplit_ReconcilesRemainder()
    {
        var rounder = new Rounder();
        var raw = new List<Share> { new("A", 10m / 3m), new("B", 10m / 3m), new("C", 10m / 3m) };

        var result = rounder.RoundShares(raw, RoundingMode.Bankers);

        CollectionAssert.AreEqual(new[] { 3.34m, 3.33m, 3.33m }, result.Select(x => x.Amount).ToArray());
        Assert.AreEqual(10.00m, result.Sum(x => x.Amount));
    }

    [TestMethod]
    public void RoundShares_EmptyShareCollection_ReturnsEmpty()
    {
        var rounder = new Rounder();

        var result = rounder.RoundShares(new List<Share>(), RoundingMode.Bankers);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Validate_CompleteBillDetails_ReturnsOk()
    {
        var validator = new BillValidator();
        var bill = new Bill(100m, 13m, TipMode.Percent, 15m);
        var attendees = new List<Attendee> { new("Alex", 1, true) };

        var result = validator.Validate(bill, attendees);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void Validate_EmptyAttendeeCollection_ReturnsFail()
    {
        var validator = new BillValidator();
        var bill = new Bill(100m, 13m, TipMode.None, 0m);

        var result = validator.Validate(bill, new List<Attendee>());

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Errors.Single(), "At least one attendee");
    }

    [TestMethod]
    public void Validate_NegativeSubtotal_ReturnsFail()
    {
        var validator = new BillValidator();
        var bill = new Bill(-1m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 1, true) };

        var result = validator.Validate(bill, attendees);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join(" ", result.Errors), "Subtotal cannot be negative");
    }

    [TestMethod]
    public void Validate_NegativeTax_ReturnsFail()
    {
        var validator = new BillValidator();
        var bill = new Bill(10m, -0.01m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 1, true) };

        var result = validator.Validate(bill, attendees);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join(" ", result.Errors), "Tax cannot be negative");
    }

    [TestMethod]
    public void Validate_ZeroAttendees_ReturnsFail()
    {
        var validator = new BillValidator();
        var bill = new Bill(0m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee>();

        var result = validator.Validate(bill, attendees);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(1, result.Errors.Count);
    }

    [TestMethod]
    public void CalculateShares_EqualSplit_ApportionsEvenly()
    {
        var splitter = new Splitter();
        var bill = new Bill(90m, 0m, TipMode.None, 0m);
        var attendees = ThreeEqualAttendees();

        var result = splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.IsTrue(result.All(share => share.Amount == 30m));
    }

    [TestMethod]
    public void CalculateShares_ProportionalSplit_ApportionsWeighted()
    {
        var splitter = new Splitter();
        var bill = new Bill(90m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 2, true), new("Blair", 1, true) };

        var result = splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.AreEqual(60m, result[0].Amount);
        Assert.AreEqual(30m, result[1].Amount);
    }

    [TestMethod]
    public void CalculateShares_ExcludedAttendee_ApportionsZero()
    {
        var splitter = new Splitter();
        var bill = new Bill(90m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 1, true), new("Blair", 1, true), new("Casey", 1, false) };

        var result = splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.AreEqual(45m, result[0].Amount);
        Assert.AreEqual(45m, result[1].Amount);
        Assert.AreEqual(0m, result[2].Amount);
    }

    [TestMethod]
    public void CalculateShares_TaxAndPercentTip_ApportionsTotal()
    {
        var splitter = new Splitter();
        var bill = new Bill(100m, 13m, TipMode.Percent, 15m);
        var attendees = new List<Attendee> { new("Alex", 1, true), new("Blair", 1, true) };

        var result = splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.AreEqual(128.00m, result.Sum(share => share.Amount));
        Assert.AreEqual(64.00m, result[0].Amount);
    }

    [TestMethod]
    public void CalculateShares_TaxAndFixedTip_ApportionsTotal()
    {
        var splitter = new Splitter();
        var bill = new Bill(100m, 13m, TipMode.Fixed, 20m);
        var attendees = new List<Attendee> { new("Alex", 1, true), new("Blair", 1, true) };

        var result = splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.AreEqual(133.00m, result.Sum(share => share.Amount));
        CollectionAssert.AreEqual(new[] { 66.50m, 66.50m }, result.Select(x => x.Amount).ToArray());
    }

    [TestMethod]
    public void CalculateShares_AllAttendeesExcluded_ThrowsException()
    {
        var splitter = new Splitter();
        var bill = new Bill(50m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 1, false) };

        void Act() => splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.ThrowsException<InvalidOperationException>(Act);
    }

    [TestMethod]
    public void CalculateShares_TotalWeightsZero_ThrowsException()
    {
        var splitter = new Splitter();
        var bill = new Bill(50m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 0, true), new("Blair", 0, true) };

        void Act() => splitter.CalculateShares(bill, attendees, RoundingMode.Bankers);

        Assert.ThrowsException<InvalidOperationException>(Act);
    }

    [TestMethod]
    public void Format_DefaultBillReceipt_ContainsLineItems()
    {
        var formatter = FixedFormatter();
        var bill = new Bill(60m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 1, true), new("Blair", 1, true) };
        var shares = new List<Share> { new("Alex", 30m), new("Blair", 30m) };

        var result = formatter.Format(bill, attendees, shares);

        StringAssert.Contains(result, "Alex [Included]: ¤30.00");
        StringAssert.Contains(result, "Blair [Included]: ¤30.00");
        StringAssert.Contains(result, "Allocated Total: ¤60.00");
    }

    [TestMethod]
    public void Format_ReceiptOutput_ContainsRequiredMetadata()
    {
        var formatter = FixedFormatter();
        var bill = new Bill(0m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", 1, true) };
        var shares = new List<Share> { new("Alex", 0m) };

        var result = formatter.Format(bill, attendees, shares);

        StringAssert.Contains(result, "Student Name: Miguel Tarazona");
        StringAssert.Contains(result, "Date: 2026-10-02");
        StringAssert.Contains(result, "Created: 2026-10-02 20:15:30 -04:00");
    }

    [TestMethod]
    [Ignore("RED/PENDING: Future feature F5 has not been implemented yet.")]
    public void Format_CsvReceiptExporter_MatchesSchema()
    {
        var expectedHeader = "Name,Amount";

        var actualCsv = "Feature F5 is pending";

        Assert.AreEqual(expectedHeader, actualCsv);
    }

    [TestMethod]
    [Ignore("RED/PENDING: Future feature F6 has not been implemented yet.")]
    public void CalculateShares_PaymentRequest_ValidatesStripeMock()
    {
        var expectedGatewayCall = true;

        var stripeMockWasCalled = false;

        Assert.AreEqual(expectedGatewayCall, stripeMockWasCalled);
    }

    [TestMethod]
    public void Validate_NegativeAttendeeWeight_ReturnsFail()
    {
        var validator = new BillValidator();
        var bill = new Bill(10m, 0m, TipMode.None, 0m);
        var attendees = new List<Attendee> { new("Alex", -1, true) };

        var result = validator.Validate(bill, attendees);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(string.Join(" ", result.Errors), "weights cannot be negative");
    }

}
