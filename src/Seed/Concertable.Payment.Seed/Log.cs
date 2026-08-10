using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Seed;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping Stripe event {EventId}: outside this E2E run")]
    internal static partial void SkippingStripeEventOutsideE2ERun(this ILogger logger, string eventId);
}
