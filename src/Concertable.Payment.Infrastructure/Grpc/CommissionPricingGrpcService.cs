using System.Globalization;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts;
using Concertable.Payment.Grpc;
using Grpc.Core;
using DomainMoney = Concertable.Kernel.ValueObjects.Money;

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
            request.Gross.ToMoney(),
            context.CancellationToken);

        return result.ValueOrRpcException().ToProto();
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
            null,
            null,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProto();
    }

    public override async Task<CommissionCalculationResponse> ConfirmReviewedGross(
        ConfirmReviewedGrossRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.ConfirmReviewedGrossAsync(
            request.BindingId.ParseOrThrow<Guid>(nameof(request.BindingId)),
            request.ExternalReference,
            request.PayerReference,
            request.ReviewedGross.ToMoney(),
            context.CancellationToken);

        return result.ValueOrRpcException().ToProto();
    }

    public override async Task<CommissionCalculationResponse> CalculateBoundCommission(
        CalculateBoundCommissionRequest request,
        ServerCallContext context)
    {
        var result = await commissionService.CalculateBoundAsync(
            request.BindingId.ParseOrThrow<Guid>(nameof(request.BindingId)),
            request.ExternalReference,
            request.PayerReference,
            request.Gross.ToMoney(),
            null,
            null,
            context.CancellationToken);

        var commission = result.ValueOrRpcException();
        return new Concertable.Payment.Contracts.CommissionCalculation(
            commission.Terms.ConfigurationId,
            commission.Terms.Rate.Value,
            DomainMoney.FromMinorUnits(commission.Calculation.PayeeGrossMinor, commission.Calculation.Currency),
            DomainMoney.FromMinorUnits(commission.Calculation.CommissionGrossMinor, commission.Calculation.Currency),
            DomainMoney.FromMinorUnits(commission.Calculation.PayerTotalMinor, commission.Calculation.Currency)).ToProto();
    }

}

internal static class CommissionPricingGrpcMappers
{
    public static CommissionCalculationResponse ToProto(
        this CommissionCalculation calculation) =>
        new()
        {
            CommissionConfigurationId = calculation.CommissionConfigurationId.ToString(),
            RatePercentage = calculation.RatePercentage.ToString(CultureInfo.InvariantCulture),
            Gross = calculation.Gross.ToProtoMoney(),
            Commission = calculation.Commission.ToProtoMoney(),
            PayerTotal = calculation.PayerTotal.ToProtoMoney()
        };

    public static CommissionBindingResponse ToProto(this CommissionBinding binding) =>
        new()
        {
            BindingId = binding.BindingId.ToString(),
            CommissionConfigurationId = binding.CommissionConfigurationId.ToString(),
            RatePercentage = binding.RatePercentage.ToString(CultureInfo.InvariantCulture),
            Currency = binding.Currency.ToProtoCurrency()
        };
}
