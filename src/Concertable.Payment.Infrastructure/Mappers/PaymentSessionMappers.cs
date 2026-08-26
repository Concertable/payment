namespace Concertable.Payment.Infrastructure.Mappers;

internal static class PaymentSessionMappers
{
    extension(PaymentSession session)
    {
        public string ToStripeUsage() => session switch
        {
            PaymentSession.OnSession => "on_session",
            PaymentSession.OffSession => "off_session",
            _ => throw new ArgumentOutOfRangeException(nameof(session), session, null)
        };
    }
}
