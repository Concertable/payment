using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class CommissionMappers
{
    public static CommissionQuote ToCommissionQuote(this Proto.CommissionQuoteResponse response) =>
        new(
            Guid.Parse(response.CommissionConfigurationId),
            response.ConfigurationVersion,
            response.RateBasisPoints,
            response.Currency.ToCurrency(),
            response.GrossMinor,
            response.CommissionMinor,
            response.PayerTotalMinor);

    public static CommissionAuthorization ToCommissionAuthorization(
        this Proto.CommissionAuthorizationResponse response) =>
        new(
            Guid.Parse(response.AuthorizationId),
            Guid.Parse(response.CommissionConfigurationId),
            response.ConfigurationVersion,
            response.RateBasisPoints,
            response.Currency.ToCurrency(),
            response.Quote?.ToCommissionQuote());

    private static Currency ToCurrency(this Proto.Currency currency) => currency switch
    {
        Proto.Currency.Gbp => Currency.Gbp,
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };
}
