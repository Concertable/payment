using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Functional = Concertable.Kernel.Functional;

namespace Concertable.Payment.Client;

public interface ICommissionClient
{
    Task<Functional.Result<CommissionQuote, CommissionError>> PreviewCommissionAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default);

    Task<Functional.Result<CommissionBinding, CommissionError>> CreateOrBindCommissionAsync(
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

    Task<Functional.Result<CommissionQuote, CommissionError>> CalculateBoundCommissionAsync(
        Guid bindingId,
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
