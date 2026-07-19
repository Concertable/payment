using Concertable.Payment.Contracts;
using Concertable.Payment.Client.Enums;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentMappers
{
    public static PaymentOutcome ToPaymentOutcome(this Proto.PaymentResponse r) =>
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

    public static PayoutAccountStatus ToStatus(this Proto.PayoutAccountStatusType status) => status switch
    {
        Proto.PayoutAccountStatusType.PayoutNotVerified => PayoutAccountStatus.NotVerified,
        Proto.PayoutAccountStatusType.PayoutPending => PayoutAccountStatus.Pending,
        Proto.PayoutAccountStatusType.PayoutVerified => PayoutAccountStatus.Verified,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
