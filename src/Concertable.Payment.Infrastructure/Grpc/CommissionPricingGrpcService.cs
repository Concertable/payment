using System.Globalization;
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

    public override async Task<CommissionCalculationResponse> PreviewCommission(
        PreviewCommissionRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.PreviewAsync(
            request.GrossMinor,
            request.Currency.ToDomainCurrency(),
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                result.Errors[0].Message));

        return result.Value.ToProto();
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

        if (result.IsFailed)
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                result.Errors[0].Message));

        return result.Value.ToProto();
    }

    public override async Task<CommissionCalculationResponse> CalculateBoundCommission(
        CalculateBoundCommissionRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.CalculateBoundAsync(
            request.BindingId.ParseOrThrow<Guid>(nameof(request.BindingId)),
            request.ExternalReference,
            request.PayerReference,
            request.Currency.ToDomainCurrency(),
            request.GrossMinor,
            EmptyToNull(request.StripePaymentIntentId),
            EmptyToNull(request.StripeSetupIntentId),
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                result.Errors[0].Message));

        return new CommissionCalculation(
            result.Value.Terms.ConfigurationId,
            result.Value.Terms.Rate.Value,
            result.Value.Binding.Currency,
            result.Value.Calculation.PayeeGrossMinor,
            result.Value.Calculation.CommissionGrossMinor,
            result.Value.Calculation.PayerTotalMinor).ToProto();
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}

internal static class CommissionPricingGrpcMappers
{
    public static CommissionCalculationResponse ToProto(
        this CommissionCalculation calculation) =>
        new()
        {
            CommissionConfigurationId = calculation.CommissionConfigurationId.ToString(),
            RatePercentage = calculation.RatePercentage.ToString(CultureInfo.InvariantCulture),
            Currency = calculation.Currency.ToProtoCurrency(),
            GrossMinor = calculation.GrossMinor,
            CommissionMinor = calculation.CommissionMinor,
            PayerTotalMinor = calculation.PayerTotalMinor
        };

    public static CommissionBindingResponse ToProto(
        this CommissionBinding binding) =>
        new()
        {
            BindingId = binding.BindingId.ToString(),
            CommissionConfigurationId = binding.CommissionConfigurationId.ToString(),
            RatePercentage = binding.RatePercentage.ToString(CultureInfo.InvariantCulture),
            Currency = binding.Currency.ToProtoCurrency(),
            Calculation = binding.Calculation?.ToProto()
        };
}
