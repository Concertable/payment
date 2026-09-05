using Reunion;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class CommissionClient : ICommissionPricingClient
{
    private readonly Proto.CommissionPricing.CommissionPricingClient client;

    public CommissionClient(Proto.CommissionPricing.CommissionPricingClient client)
    {
        this.client = client;
    }

    public Task<Result<CommissionCalculation, CommissionError>> PreviewAsync(
        Money gross,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PreviewCommissionAsync(
                Proto.PreviewCommissionRequest.Create(gross),
                cancellationToken: ct)).ToCommissionCalculation(),
            error => error.ToCommissionError(),
            ct);

    public Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var request = Proto.CreateOrBindCommissionRequest.Create(
                    externalReference,
                    payerReference,
                    currency,
                    reviewedCommissionConfigurationId);
                return (await client.CreateOrBindCommissionAsync(request, cancellationToken: ct)).ToCommissionBinding();
            },
            error => error.ToCommissionError(),
            ct);

    public Task<Result<CommissionCalculation, CommissionError>> ConfirmReviewedGrossAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money reviewedGross,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.ConfirmReviewedGrossAsync(
                Proto.ConfirmReviewedGrossRequest.Create(
                    bindingId,
                    externalReference,
                    payerReference,
                    reviewedGross),
                cancellationToken: ct)).ToCommissionCalculation(),
            error => error.ToCommissionError(),
            ct);

    public Task<Result<CommissionCalculation, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money gross,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CalculateBoundCommissionAsync(
                Proto.CalculateBoundCommissionRequest.Create(
                    bindingId,
                    externalReference,
                    payerReference,
                    gross),
                cancellationToken: ct)).ToCommissionCalculation(),
            error => error.ToCommissionError(),
            ct);

}
