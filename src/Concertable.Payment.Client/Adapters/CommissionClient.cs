using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class CommissionClient : ICommissionPricingClient, ICommissionClient
{
    private readonly Proto.CommissionPricing.CommissionPricingClient client;

    public CommissionClient(Proto.CommissionPricing.CommissionPricingClient client)
    {
        this.client = client;
    }

    public Task<Result<CommissionCalculation, CommissionError>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PreviewCommissionAsync(
                new Proto.PreviewCommissionRequest
                {
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency()
                },
                cancellationToken: ct)).ToCommissionCalculation(),
            CommissionError.FromCode,
            ct);

    public Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        long? grossMinor = null,
        long? expectedCommissionMinor = null,
        long? expectedPayerTotalMinor = null,
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
                if (grossMinor is not null)
                    request.GrossMinor = grossMinor.Value;
                if (expectedCommissionMinor is not null)
                    request.ExpectedCommissionMinor = expectedCommissionMinor.Value;
                if (expectedPayerTotalMinor is not null)
                    request.ExpectedPayerTotalMinor = expectedPayerTotalMinor.Value;
                return (await client.CreateOrBindCommissionAsync(request, cancellationToken: ct)).ToCommissionBinding();
            },
            CommissionError.FromCode,
            ct);

    public Task<Result<CommissionCalculation, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
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
                    Currency = currency.ToProtoCurrency(),
                    GrossMinor = grossMinor,
                    StripePaymentIntentId = stripePaymentIntentId ?? string.Empty,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                },
                cancellationToken: ct)).ToCommissionCalculation(),
            CommissionError.FromCode,
            ct);

    async Task<FluentResults.Result<CommissionCalculation>> ICommissionClient.PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct) =>
        (await PreviewAsync(grossMinor, currency, ct)).ToLegacy();

    async Task<FluentResults.Result<CommissionBinding>> ICommissionClient.CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor,
        long? expectedCommissionMinor,
        long? expectedPayerTotalMinor,
        CancellationToken ct) =>
        (await CreateOrBindAsync(
            externalReference,
            payerReference,
            currency,
            reviewedCommissionConfigurationId,
            stripePaymentIntentId,
            stripeSetupIntentId,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor,
            ct)).ToLegacy();

    async Task<FluentResults.Result<CommissionCalculation>> ICommissionClient.CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct) =>
        (await CalculateBoundAsync(
            bindingId,
            externalReference,
            payerReference,
            currency,
            grossMinor,
            stripePaymentIntentId,
            stripeSetupIntentId,
            ct)).ToLegacy();
}
