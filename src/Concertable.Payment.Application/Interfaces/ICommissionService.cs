using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionService
{
    Task<Result<CommissionQuote, CommissionError>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default);

    Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor,
        long? expectedCommissionMinor,
        long? expectedPayerTotalMinor,
        CancellationToken ct = default);

    Task<Result<BoundCommission, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<string?> FindBoundPaymentIntentAsync(
        Guid bindingId,
        CancellationToken ct = default);

    void BindPaymentIntent(
        CommissionBindingEntity binding,
        string paymentIntentId);
}
