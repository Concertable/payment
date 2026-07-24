using Concertable.Payment.Grpc;
using Money = Concertable.Kernel.ValueObjects.Money;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record DepositCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId);

internal sealed record CaptureCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    string PaymentIntentId,
    int BookingId);

internal static class EscrowRequestMappers
{
    public static DepositCommand ToCommand(this DepositRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId);

    public static CaptureCommand ToCommand(this CaptureRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentIntentId,
        request.BookingId);
}
