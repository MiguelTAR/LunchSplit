using RestaurantBillSplitter.Core.Payments;
using Square;
using Square.Payments;

namespace RestaurantBillSplitter.Infrastructure.Payments;

public sealed class SquarePaymentService : IPaymentProcessor
{
    private readonly SquareClient _client;
    private readonly string _locationId;

    public SquarePaymentService(
        string? accessToken = null,
        string? locationId = null,
        TimeSpan? timeout = null)
    {
        accessToken ??= Environment.GetEnvironmentVariable("SQUARE_SANDBOX_ACCESS_TOKEN");
        locationId ??= Environment.GetEnvironmentVariable("SQUARE_SANDBOX_LOCATION_ID");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "Square Sandbox access token is missing. Set SQUARE_SANDBOX_ACCESS_TOKEN.");
        }

        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new InvalidOperationException(
                "Square Sandbox location ID is missing. Set SQUARE_SANDBOX_LOCATION_ID.");
        }

        _locationId = locationId;
        _client = new SquareClient(
            accessToken,
            new ClientOptions
            {
                BaseUrl = SquareEnvironment.Sandbox,
                Timeout = timeout ?? TimeSpan.FromSeconds(5)
            });
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var response = await _client.Payments.CreateAsync(
            new CreatePaymentRequest
            {
                SourceId = request.CardNonce,
                IdempotencyKey = request.IdempotencyKey,
                AmountMoney = new Money
                {
                    Amount = ToMinorUnits(request.Amount),
                    Currency = Currency.FromCustom(request.CurrencyCode.ToUpperInvariant())
                },
                LocationId = request.LocationId ?? _locationId,
                Autocomplete = true
            });

        var payment = response.Payment;
        return new PaymentResult(
            IsSuccess: payment is not null,
            Status: ParseStatus(payment?.Status),
            TransactionId: payment?.Id,
            ErrorCode: response.Errors?.FirstOrDefault()?.Code.ToString());
    }

    public async Task<PaymentStatus> GetTransactionStatusAsync(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new ArgumentException("Transaction ID is required.", nameof(transactionId));
        }

        var response = await _client.Payments.GetAsync(
            new GetPaymentsRequest { PaymentId = transactionId });

        return ParseStatus(response.Payment?.Status);
    }

    private static void ValidateRequest(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Amount), "Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            throw new ArgumentException("Currency code is required.", nameof(request.CurrencyCode));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(request.IdempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(request.CardNonce))
        {
            throw new ArgumentException("Card nonce is required.", nameof(request.CardNonce));
        }
    }

    private static long ToMinorUnits(decimal amount)
    {
        var minorUnits = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        return checked((long)minorUnits);
    }

    private static PaymentStatus ParseStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "PENDING" => PaymentStatus.Pending,
            "APPROVED" => PaymentStatus.Approved,
            "COMPLETED" => PaymentStatus.Completed,
            "FAILED" => PaymentStatus.Failed,
            "CANCELED" or "CANCELLED" => PaymentStatus.Canceled,
            _ => PaymentStatus.Unknown
        };
}
