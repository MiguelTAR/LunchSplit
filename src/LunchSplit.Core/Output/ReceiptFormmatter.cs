using System.Globalization;
using System.Text;
using RestaurantBillSplitter.Core.Models;

namespace RestaurantBillSplitter.Core.Output;

public sealed class ReceiptFormatter
{
    private const string StudentName = "Miguel Tarazona";
    private readonly Func<DateTimeOffset> _clock;

    public ReceiptFormatter() : this(() => DateTimeOffset.Now)
    {
    }

    public ReceiptFormatter(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string Format(Bill bill, List<Attendee> attendees, List<Share> roundedShares)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(attendees);
        ArgumentNullException.ThrowIfNull(roundedShares);

        var created = _clock();
        var builder = new StringBuilder();
        builder.AppendLine("RESTAURANT BILL SPLIT RECEIPT");
        builder.AppendLine($"Student Name: {StudentName}");
        builder.AppendLine($"Date: {created:yyyy-MM-dd}");
        builder.AppendLine($"Created: {created:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine(new string('-', 42));
        builder.AppendLine($"Subtotal: {bill.Subtotal.ToString("C2", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Tax: {bill.Tax.ToString("C2", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Tip Mode: {bill.TipMode}");
        builder.AppendLine($"Tip Input: {bill.TipInput.ToString("0.##", CultureInfo.InvariantCulture)}");
        builder.AppendLine(new string('-', 42));

        foreach (var share in roundedShares)
        {
            var attendee = attendees.FirstOrDefault(item => item.Name == share.Name);
            var status = attendee is null ? "Unknown" : attendee.Included ? "Included" : "Excluded";
            builder.AppendLine($"{share.Name} [{status}]: {share.Amount.ToString("C2", CultureInfo.InvariantCulture)}");
        }

        builder.AppendLine(new string('-', 42));
        builder.AppendLine($"Allocated Total: {roundedShares.Sum(share => share.Amount).ToString("C2", CultureInfo.InvariantCulture)}");
        return builder.ToString();
    }
}
