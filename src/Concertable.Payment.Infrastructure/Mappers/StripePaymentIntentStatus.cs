namespace Concertable.Payment.Infrastructure.Mappers;

internal static class StripePaymentIntentStatus
{
    public const string Succeeded = "succeeded";
    public const string RequiresAction = "requires_action";
    public const string RequiresConfirmation = "requires_confirmation";
}
