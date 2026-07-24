using Concertable.Kernel.ValueObjects;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class MoneyMappers
{
    public static Money ToMoney(this Proto.Money? money) =>
        money is null
            ? throw new RpcException(new Status(StatusCode.InvalidArgument, "Money amount is required."))
            : Money.FromMinorUnits(money.AmountMinor, money.Currency.ToDomainCurrency());

    public static Currency ToDomainCurrency(this Proto.Currency currency) => currency switch
    {
        Proto.Currency.Gbp => Currency.Gbp,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };
}
