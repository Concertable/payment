using Concertable.Payment.Grpc;
using Money = Concertable.Kernel.ValueObjects.Money;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed record ManagerPayCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId);

internal sealed record CommissionAuthorizedManagerPayCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Gross,
    string PaymentMethodId,
    PaymentSession Session,
    int BookingId,
    Guid CommissionAuthorizationId,
    string ExternalReference,
    long ExpectedCommissionMinor,
    long ExpectedPayerTotalMinor,
    string? StripeSetupIntentId);

internal sealed record CreateSessionCommand(
    Guid PayerId,
    IReadOnlyDictionary<string, string> Metadata);

internal sealed record CreateHoldSessionCommand(
    Guid PayerId,
    Money Amount,
    IReadOnlyDictionary<string, string> Metadata);

internal sealed record CreateCommissionAuthorizedHoldSessionCommand(
    Guid PayerId,
    Money Gross,
    IReadOnlyDictionary<string, string> Metadata,
    Guid CommissionAuthorizationId,
    string ExternalReference,
    long ExpectedCommissionMinor,
    long ExpectedPayerTotalMinor,
    string? StripeSetupIntentId);

internal sealed record FindHeldIntentCommand(
    Guid PayerId,
    int ApplicationId);

internal static class ManagerPaymentRequestMappers
{
    public static ManagerPayCommand ToCommand(this ManagerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId);

    public static CommissionAuthorizedManagerPayCommand ToCommand(
        this CommissionAuthorizedManagerPayRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        Money.FromMinorUnits(request.GrossMinor, request.Currency.ToDomainCurrency()),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId,
        request.CommissionAuthorizationId.ParseOrThrow<Guid>(
            nameof(request.CommissionAuthorizationId)),
        request.ExternalReference,
        request.ExpectedCommissionMinor,
        request.ExpectedPayerTotalMinor,
        EmptyToNull(request.StripeSetupIntentId));

    public static CreateSessionCommand ToCommand(this CreateSetupSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Metadata);

    public static CreateSessionCommand ToCommand(this CreateVerifySessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Metadata);

    public static CreateHoldSessionCommand ToCommand(this CreateHoldSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.Amount.ToMoney(),
        request.Metadata);

    public static CreateCommissionAuthorizedHoldSessionCommand ToCommand(
        this CreateCommissionAuthorizedHoldSessionRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        Money.FromMinorUnits(request.GrossMinor, request.Currency.ToDomainCurrency()),
        request.Metadata,
        request.CommissionAuthorizationId.ParseOrThrow<Guid>(
            nameof(request.CommissionAuthorizationId)),
        request.ExternalReference,
        request.ExpectedCommissionMinor,
        request.ExpectedPayerTotalMinor,
        EmptyToNull(request.StripeSetupIntentId));

    public static FindHeldIntentCommand ToCommand(this FindHeldIntentRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.ApplicationId);

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
