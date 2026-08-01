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

    Task<Result<CommissionBinding>> CreateOrBindAsync(
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

    Task<Result<CommissionQuote>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default);
}
