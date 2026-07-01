using Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record ManagerPayCommand(
    Guid PayerId,
    Guid PayeeId,
    decimal Amount,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId);

internal sealed record CreateSessionCommand(
    Guid PayerId,
    IDictionary<string, string> Metadata);

internal sealed record CreateHoldSessionCommand(
    Guid PayerId,
    decimal Amount,
    IDictionary<string, string> Metadata);

internal sealed record FindHeldIntentCommand(
    Guid PayerId,
    int ApplicationId);

internal static class ManagerPaymentRequestMappers
{
    public static ManagerPayCommand ToCommand(this ManagerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ParseOrThrow<decimal>(nameof(request.Amount)),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId);

    public static CreateSessionCommand ToCommand(this CreateSetupSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Metadata);

    public static CreateSessionCommand ToCommand(this CreateVerifySessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Metadata);

    public static CreateHoldSessionCommand ToCommand(this CreateHoldSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Amount.ParseOrThrow<decimal>(nameof(request.Amount)),
        request.Metadata);

    public static FindHeldIntentCommand ToCommand(this FindHeldIntentRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ApplicationId);
}
