using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionService
{
    Task<Result<CommissionCalculation, CommissionError>> PreviewAsync(
        Money gross,
        CancellationToken ct = default);

    Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<Result<CommissionCalculation, CommissionError>> ConfirmReviewedGrossAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money reviewedGross,
        CancellationToken ct = default);

    Task<Result<BoundCommission, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money gross,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<Option<string>> FindBoundPaymentIntentAsync(
        Guid bindingId,
        CancellationToken ct = default);

    void BindPaymentIntent(CommissionBindingEntity binding, string paymentIntentId);
}
