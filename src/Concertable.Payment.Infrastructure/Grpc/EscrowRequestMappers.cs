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

internal sealed record CommissionAuthorizedDepositCommand(
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

internal sealed record CaptureCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    string PaymentIntentId,
    int BookingId);

internal sealed record CommissionAuthorizedCaptureCommand(
    Guid PayerId,
    Guid PayeeId,
    Money Gross,
    string PaymentIntentId,
    int BookingId,
    Guid CommissionAuthorizationId,
    string ExternalReference,
    long ExpectedCommissionMinor,
    long ExpectedPayerTotalMinor);

internal static class EscrowRequestMappers
{
    public static DepositCommand ToCommand(this DepositRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentMethodId,
        request.Session.ToPaymentSession(),
        request.BookingId);

    public static CommissionAuthorizedDepositCommand ToCommand(
        this CommissionAuthorizedDepositRequest request) => new(
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

    public static CaptureCommand ToCommand(this CaptureRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        request.Amount.ToMoney(),
        request.PaymentIntentId,
        request.BookingId);

    public static CommissionAuthorizedCaptureCommand ToCommand(
        this CommissionAuthorizedCaptureRequest request) => new(
        request.PayerId.ParseOrThrow<Guid>(nameof(request.PayerId)),
        request.PayeeId.ParseOrThrow<Guid>(nameof(request.PayeeId)),
        Money.FromMinorUnits(request.GrossMinor, request.Currency.ToDomainCurrency()),
        request.PaymentIntentId,
        request.BookingId,
        request.CommissionAuthorizationId.ParseOrThrow<Guid>(
            nameof(request.CommissionAuthorizationId)),
        request.ExternalReference,
        request.ExpectedCommissionMinor,
        request.ExpectedPayerTotalMinor);

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
