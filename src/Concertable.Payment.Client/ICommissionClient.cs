using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using FluentResults;

namespace Concertable.Payment.Client;

public interface ICommissionClient
{
    Task<Result<CommissionQuote>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default);

    Task<Result<CommissionAuthorization>> CreateOrBindAuthorizationAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        long? grossMinor = null,
        long? expectedCommissionMinor = null,
        long? expectedPayerTotalMinor = null,
        CancellationToken ct = default);

    Task<Result<CommissionQuote>> CalculateAuthorizedAsync(
        Guid authorizationId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default);
}
