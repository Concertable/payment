using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class CommissionPricingGrpcService : CommissionPricing.CommissionPricingBase
{
    private readonly ICommissionService commissionService;

    public CommissionPricingGrpcService(ICommissionService commissionService)
    {
        this.commissionService = commissionService;
    }

    public override async Task<CommissionQuoteResponse> PreviewCommission(
        PreviewCommissionRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.PreviewAsync(
            request.GrossMinor,
            request.Currency.ToDomainCurrency(),
            context.CancellationToken);

        return result.GetValueOrThrow().ToProto();
    }

    public override async Task<CommissionBindingResponse> CreateOrBindCommission(
        CreateOrBindCommissionRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.CreateOrBindAsync(
            request.ExternalReference,
            request.PayerReference,
            request.Currency.ToDomainCurrency(),
            request.ReviewedCommissionConfigurationId.ParseOrThrow<Guid>(
                nameof(request.ReviewedCommissionConfigurationId)),
            EmptyToNull(request.StripePaymentIntentId),
            EmptyToNull(request.StripeSetupIntentId),
            request.HasGrossMinor ? request.GrossMinor : null,
            request.HasExpectedCommissionMinor ? request.ExpectedCommissionMinor : null,
            request.HasExpectedPayerTotalMinor ? request.ExpectedPayerTotalMinor : null,
            context.CancellationToken);

        return result.GetValueOrThrow().ToProto();
    }

    public override async Task<CommissionQuoteResponse> CalculateBoundCommission(
        CalculateBoundCommissionRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.CalculateBoundAsync(
            request.BindingId.ParseOrThrow<Guid>(nameof(request.BindingId)),
            request.ExternalReference,
            request.PayerReference,
            request.Currency.ToDomainCurrency(),
            request.GrossMinor,
            request.ExpectedCommissionMinor,
            request.ExpectedPayerTotalMinor,
            EmptyToNull(request.StripePaymentIntentId),
            EmptyToNull(request.StripeSetupIntentId),
            context.CancellationToken);

        var commission = result.GetValueOrThrow();
        return new CommissionQuote(
            commission.Terms.ConfigurationId,
            commission.Terms.Version,
            commission.Terms.RateBasisPoints,
            commission.Terms.Currency,
            commission.Calculation.PayeeGrossMinor,
            commission.Calculation.CommissionGrossMinor,
            commission.Calculation.PayerTotalMinor).ToProto();
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}

internal static class CommissionPricingGrpcMappers
{
    public static CommissionQuoteResponse ToProto(this CommissionQuote quote) =>
        new()
        {
            CommissionConfigurationId = quote.CommissionConfigurationId.ToString(),
            ConfigurationVersion = quote.ConfigurationVersion,
            RateBasisPoints = quote.RateBasisPoints,
            Currency = quote.Currency.ToProtoCurrency(),
            GrossMinor = quote.GrossMinor,
            CommissionMinor = quote.CommissionMinor,
            PayerTotalMinor = quote.PayerTotalMinor
        };

    public static CommissionBindingResponse ToProto(
        this CommissionBinding binding) =>
        new()
        {
            BindingId = binding.BindingId.ToString(),
            CommissionConfigurationId = binding.CommissionConfigurationId.ToString(),
            ConfigurationVersion = binding.ConfigurationVersion,
            RateBasisPoints = binding.RateBasisPoints,
            Currency = binding.Currency.ToProtoCurrency(),
            Quote = binding.Quote?.ToProto()
        };
}
