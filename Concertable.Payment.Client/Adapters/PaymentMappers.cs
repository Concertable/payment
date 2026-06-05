using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentMappers
{
    public static PaymentResponse ToPaymentResponse(this Proto.PaymentResponse r) =>
        new()
        {
            RequiresAction = r.RequiresAction,
            ClientSecret = string.IsNullOrEmpty(r.ClientSecret) ? null : r.ClientSecret,
            TransactionId = string.IsNullOrEmpty(r.TransactionId) ? null : r.TransactionId
        };

    public static CheckoutSession ToCheckoutSession(this Proto.CheckoutSessionResponse r) =>
        new(r.ClientSecret, r.CustomerSession, r.CustomerId);

    public static Proto.PaymentSessionType ToProtoSession(this PaymentSession session) => session switch
    {
        PaymentSession.OnSession => Proto.PaymentSessionType.OnSession,
        PaymentSession.OffSession => Proto.PaymentSessionType.OffSession,
        _ => throw new ArgumentOutOfRangeException(nameof(session), session, null)
    };
}
