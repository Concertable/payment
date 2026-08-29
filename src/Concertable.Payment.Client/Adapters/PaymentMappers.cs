using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Client.Enums;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentMappers
{
    public static Proto.Money ToProtoMoney(this Money money) => new()
    {
        AmountMinor = money.ToMinorUnits(),
        Currency = money.Currency.ToProtoCurrency()
    };

    public static Money ToMoney(this Proto.Money money) =>
        Money.FromMinorUnits(money.AmountMinor, money.Currency.ToCurrency());

    public static Proto.Currency ToProtoCurrency(this Currency currency) => currency switch
    {
        Currency.Gbp => Proto.Currency.Gbp,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };

    private static Currency ToCurrency(this Proto.Currency currency) => currency switch
    {
        Proto.Currency.Gbp => Currency.Gbp,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };

    public static PaymentOutcome ToPaymentOutcome(this Proto.PaymentResponse r) =>
        new()
        {
            RequiresAction = r.RequiresAction,
            ClientSecret = r.HasClientSecret ? r.ClientSecret : null,
            TransactionId = r.TransactionId
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

    public static MonthlyPaymentPoint ToMonthlyPaymentPoint(this Proto.MonthlyPaymentPointResponse point) =>
        new(
            DateOnly.FromDateTime(point.Month.ToDateTime()),
            point.Gross.ToMoney(),
            point.Net.ToMoney(),
            point.Count);

    public static ManagerSettlement ToManagerSettlement(this Proto.SettlementReportItemResponse settlement) =>
        new(
            settlement.Id,
            settlement.BookingId,
            Guid.Parse(settlement.PayerId),
            Guid.Parse(settlement.PayeeId),
            settlement.Amount.ToMoney(),
            settlement.At.ToDateTime());
}
