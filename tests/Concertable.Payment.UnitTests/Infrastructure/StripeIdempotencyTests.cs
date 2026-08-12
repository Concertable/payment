using Concertable.Payment.Infrastructure.Services;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeIdempotencyTests
{
    [Fact]
    public void FromMetadata_BookingOperation_ReturnsStableBookingKey()
    {
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.BookingId] = "17"
        };

        var options = StripeIdempotency.FromMetadata(metadata, "capture");

        Assert.NotNull(options);
        Assert.Equal("booking:17:capture", options.IdempotencyKey);
    }

    [Fact]
    public void FromMetadata_CommissionBinding_PrefersBindingKey()
    {
        var bindingId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.BookingId] = "17",
            [PaymentMetadataKeys.CommissionBindingId] = bindingId.ToString()
        };

        var options = StripeIdempotency.FromMetadata(metadata, "refund:5000");

        Assert.NotNull(options);
        Assert.Equal($"commission:{bindingId}:refund:5000", options.IdempotencyKey);
    }
}
