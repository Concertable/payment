using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using FluentResults;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class CommissionClient : ICommissionClient
{
    private readonly Proto.CommissionPricing.CommissionPricingClient client;

    public CommissionClient(Proto.CommissionPricing.CommissionPricingClient client)
    {
        this.client = client;
    }

    public async Task<Result<CommissionQuote>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default)
    {
        try
        {
            var response = await client.PreviewCommissionAsync(
                new Proto.PreviewCommissionRequest
                {
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency()
                },
                cancellationToken: ct);
            return Result.Ok(response.ToCommissionQuote());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<CommissionBinding>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        long? grossMinor = null,
        long? expectedCommissionMinor = null,
        long? expectedPayerTotalMinor = null,
        CancellationToken ct = default)
    {
        try
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

            var response = await client.CreateOrBindCommissionAsync(
                request,
                cancellationToken: ct);
            return Result.Ok(response.ToCommissionBinding());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<CommissionQuote>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.CalculateBoundCommissionRequest
            {
                BindingId = bindingId.ToString(),
                ExternalReference = externalReference,
                PayerReference = payerReference,
                Currency = currency.ToProtoCurrency(),
                GrossMinor = grossMinor,
                StripePaymentIntentId = stripePaymentIntentId ?? string.Empty,
                StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
            };

            var response = await client.CalculateBoundCommissionAsync(
                request,
                cancellationToken: ct);
            return Result.Ok(response.ToCommissionQuote());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }
}
