using Concertable.Payment.Grpc;
using Money = Concertable.Kernel.ValueObjects.Money;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record CustomerPayCommand(
    Guid PayerId,
    int ConcertId,
    Guid PayeeId,
    Money Amount,
    IReadOnlyDictionary<string, string> Metadata,
    string PaymentMethodId);

internal sealed record CreatePaymentSessionCommand(
    Guid PayerId,
    int ConcertId,
    Guid PayeeId,
    IReadOnlyDictionary<string, string> Metadata);

internal static class CustomerPaymentRequestMappers
{
    public static CustomerPayCommand ToCommand(this CustomerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ConcertId,
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.Metadata,
        request.PaymentMethodId);

    public static CreatePaymentSessionCommand ToCommand(this CreatePaymentSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ConcertId,
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Metadata);
}
