using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeIdempotency
{
    public static RequestOptions? FromMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string operation)
    {
        if (metadata.TryGetValue(PaymentMetadataKeys.CommissionBindingId, out var bindingId))
            return new RequestOptions
            {
                IdempotencyKey = $"commission:{bindingId}:{operation}"
            };

        if (!metadata.TryGetValue(PaymentMetadataKeys.BookingId, out var bookingId))
            return null;

        return new RequestOptions
        {
            IdempotencyKey = $"booking:{bookingId}:{operation}"
        };
    }
}
