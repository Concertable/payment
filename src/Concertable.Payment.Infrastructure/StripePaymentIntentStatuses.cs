namespace Concertable.Payment.Infrastructure;

internal static class StripePaymentIntentStatuses
{
    public const string Succeeded = "succeeded";
    public const string RequiresAction = "requires_action";
    public const string RequiresConfirmation = "requires_confirmation";
}
