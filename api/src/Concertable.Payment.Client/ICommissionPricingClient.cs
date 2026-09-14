using Reunion;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Client;

public interface ICommissionPricingClient
{
    Task<Result<CommissionCalculation, CommissionError>> PreviewAsync(
        Money gross,
        CancellationToken ct = default);

    Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        CancellationToken ct = default);

    Task<Result<CommissionCalculation, CommissionError>> ConfirmReviewedGrossAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money reviewedGross,
        CancellationToken ct = default);

    Task<Result<CommissionCalculation, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money gross,
        CancellationToken ct = default);
}
