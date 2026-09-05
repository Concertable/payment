using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Client.Enums;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentMappers
{
    extension(Money money)
    {
        public Proto.Money ToProtoMoney() => new()
        {
            AmountMinor = money.ToMinorUnits(),
            Currency = money.Currency.ToProtoCurrency()
        };
    }

    extension(Proto.Money money)
    {
        public Money ToMoney() => Money.FromMinorUnits(money.AmountMinor, money.Currency.ToCurrency());
    }

    extension(Currency currency)
    {
        public Proto.Currency ToProtoCurrency() => currency switch
        {
            Currency.Gbp => Proto.Currency.Gbp,
            _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
        };
    }

    extension(Proto.Currency currency)
    {
        private Currency ToCurrency() => currency switch
        {
            Proto.Currency.Gbp => Currency.Gbp,
            _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
        };
    }

    extension(Proto.PaymentResponse response)
    {
        public PaymentOutcome ToPaymentOutcome() => new()
        {
            RequiresAction = response.RequiresAction,
            ClientSecret = response.HasClientSecret ? response.ClientSecret : null
        };
    }

    extension(PaymentSession session)
    {
        public Proto.PaymentSessionType ToProtoSession() => session switch
        {
            PaymentSession.OnSession => Proto.PaymentSessionType.OnSession,
            PaymentSession.OffSession => Proto.PaymentSessionType.OffSession,
            _ => throw new ArgumentOutOfRangeException(nameof(session), session, null)
        };
    }

    extension(Proto.PayoutAccountStatusType status)
    {
        public PayoutAccountStatus ToStatus() => status switch
        {
            Proto.PayoutAccountStatusType.PayoutNotVerified => PayoutAccountStatus.NotVerified,
            Proto.PayoutAccountStatusType.PayoutPending => PayoutAccountStatus.Pending,
            Proto.PayoutAccountStatusType.PayoutVerified => PayoutAccountStatus.Verified,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    extension(Proto.MonthlyPaymentPointResponse point)
    {
        public MonthlyPaymentPoint ToMonthlyPaymentPoint() => new(
            DateOnly.FromDateTime(point.Month.ToDateTime()),
            point.Gross.ToMoney(),
            point.Net.ToMoney(),
            point.Count);
    }

    extension(Proto.SettlementReportItemResponse settlement)
    {
        public PaymentSettlement ToPaymentSettlement() => new(
            settlement.Id,
            new(
                settlement.Reference.OperationType,
                settlement.Reference.ClientReference),
            Guid.Parse(settlement.PayerId),
            Guid.Parse(settlement.PayeeId),
            settlement.Amount.ToMoney(),
            settlement.At.ToDateTime());
    }
}
