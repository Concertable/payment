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
                new Proto.PreviewCommissionRequest
                {
                    Gross = gross.ToProtoMoney()
                },
                cancellationToken: ct)).ToCommissionCalculation(),
            error => error.ToCommissionError(),
            ct);

    public Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var request = new Proto.CreateOrBindCommissionRequest
                {
                    ExternalReference = externalReference,
                    PayerReference = payerReference,
                    Currency = currency.ToProtoCurrency(),
                    ReviewedCommissionConfigurationId = reviewedCommissionConfigurationId.ToString(),
                    StripePaymentIntentId = stripePaymentIntentId ?? string.Empty,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                };
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
                new Proto.ConfirmReviewedGrossRequest
                {
                    BindingId = bindingId.ToString(),
                    ExternalReference = externalReference,
                    PayerReference = payerReference,
                    ReviewedGross = reviewedGross.ToProtoMoney()
                },
                cancellationToken: ct)).ToCommissionCalculation(),
            error => error.ToCommissionError(),
            ct);

    public Task<Result<CommissionCalculation, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money gross,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CalculateBoundCommissionAsync(
                new Proto.CalculateBoundCommissionRequest
                {
                    BindingId = bindingId.ToString(),
                    ExternalReference = externalReference,
                    PayerReference = payerReference,
                    Gross = gross.ToProtoMoney(),
                    StripePaymentIntentId = stripePaymentIntentId ?? string.Empty,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                },
                cancellationToken: ct)).ToCommissionCalculation(),
            error => error.ToCommissionError(),
            ct);

}
