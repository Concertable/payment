using System.Globalization;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class CommissionMappers
{
    public static CommissionCalculation ToCommissionCalculation(
        this Proto.CommissionCalculationResponse response) =>
        new(
            Guid.Parse(response.CommissionConfigurationId),
            decimal.Parse(response.RatePercentage, CultureInfo.InvariantCulture),
            response.Gross.ToMoney(),
            response.Commission.ToMoney(),
            response.PayerTotal.ToMoney());

    public static CommissionBinding ToCommissionBinding(
        this Proto.CommissionBindingResponse response) =>
        new(
            Guid.Parse(response.BindingId),
            Guid.Parse(response.CommissionConfigurationId),
            decimal.Parse(response.RatePercentage, CultureInfo.InvariantCulture),
            response.Currency.ToCurrency());

    private static Currency ToCurrency(this Proto.Currency currency) => currency switch
    {
        Proto.Currency.Gbp => Currency.Gbp,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };
}
