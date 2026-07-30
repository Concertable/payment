using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeIdempotency
{
    public static RequestOptions? FromMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string operation)
    {
        if (!metadata.TryGetValue(PaymentMetadataKeys.CommissionAuthorizationId, out var authorizationId))
            return null;

        return new RequestOptions
        {
            IdempotencyKey = $"commission:{authorizationId}:{operation}"
        };
    }
}
