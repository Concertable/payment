using Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record DepositCommand(
    Guid PayerId,
    Guid PayeeId,
    decimal Amount,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId);

internal sealed record CaptureCommand(
    Guid PayerId,
    Guid PayeeId,
    decimal Amount,
    string PaymentIntentId,
    int BookingId);

internal static class EscrowRequestMappers
{
    public static DepositCommand ToCommand(this DepositRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ParseOrThrow<decimal>(nameof(request.Amount)),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId);

    public static CaptureCommand ToCommand(this CaptureRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ParseOrThrow<decimal>(nameof(request.Amount)),
        request.PaymentIntentId,
        request.BookingId);
}
