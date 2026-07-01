using Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record CustomerPayCommand(
    Guid PayerId,
    int ConcertId,
    Guid PayeeId,
    decimal Amount,
    IDictionary<string, string> Metadata,
    string PaymentMethodId);

internal sealed record CreatePaymentSessionCommand(
    Guid PayerId,
    int ConcertId,
    Guid PayeeId,
    IDictionary<string, string> Metadata);

internal static class CustomerPaymentRequestMappers
{
    public static CustomerPayCommand ToCommand(this CustomerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ConcertId,
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ParseOrThrow<decimal>(nameof(request.Amount)),
        request.Metadata,
        request.PaymentMethodId);

    public static CreatePaymentSessionCommand ToCommand(this CreatePaymentSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ConcertId,
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Metadata);
}
