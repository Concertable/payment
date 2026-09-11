using Concertable.Payment.Grpc;
using PayoutAccountStatus = Concertable.Payment.Application.Enums.PayoutAccountStatus;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class PaymentMappers
{
    extension(PayoutAccountStatus status)
    {
        public PayoutAccountStatusType ToProtoStatus() => status switch
        {
            PayoutAccountStatus.NotVerified => PayoutAccountStatusType.PayoutNotVerified,
            PayoutAccountStatus.Pending => PayoutAccountStatusType.PayoutPending,
            PayoutAccountStatus.Verified => PayoutAccountStatusType.PayoutVerified,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    extension(PaymentOutcome outcome)
    {
        public PaymentResponse ToProtoPaymentResponse()
        {
            var message = new PaymentResponse { RequiresAction = outcome.RequiresAction };
            if (outcome.ClientSecret is { } clientSecret)
                message.ClientSecret = clientSecret;

            return message;
        }
    }

    extension(PaymentSessionType session)
    {
        public PaymentSession ToPaymentSession() =>
            session == PaymentSessionType.OffSession ? PaymentSession.OffSession : PaymentSession.OnSession;
    }
}
