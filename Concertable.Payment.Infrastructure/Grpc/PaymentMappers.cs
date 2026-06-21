using Concertable.Payment.Domain;
using Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class PaymentMappers
{
    public static PayoutAccountStatusType ToProtoStatus(this PayoutAccountStatus status) => status switch
    {
        PayoutAccountStatus.NotVerified => PayoutAccountStatusType.PayoutNotVerified,
        PayoutAccountStatus.Pending => PayoutAccountStatusType.PayoutPending,
        PayoutAccountStatus.Verified => PayoutAccountStatusType.PayoutVerified,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static PaymentResponse ToProtoPaymentResponse(this Application.DTOs.PaymentResponse r) =>
        new()
        {
            RequiresAction = r.RequiresAction,
            ClientSecret = r.ClientSecret ?? "",
            TransactionId = r.TransactionId ?? ""
        };

    public static CheckoutSessionResponse ToProtoCheckoutSession(this Application.DTOs.CheckoutSession s) =>
        new() { ClientSecret = s.ClientSecret, CustomerSession = s.CustomerSession, CustomerId = s.CustomerId };

    public static PaymentSession ToPaymentSession(this PaymentSessionType session) =>
        session == PaymentSessionType.OffSession ? PaymentSession.OffSession : PaymentSession.OnSession;
}
