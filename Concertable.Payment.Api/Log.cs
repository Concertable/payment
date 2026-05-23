using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Api;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "[WebhookController] webhook received bytes={Bytes}")]
    internal static partial void WebhookReceived(this ILogger logger, int bytes);
}
