using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using FluentResults;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionService : ICommissionService
{
    private readonly ICommissionBindingRepository bindingRepository;
    private readonly ICommissionConfigurationRepository configurationRepository;
    private readonly CommissionCalculator calculator;
    private readonly Guid currentConfigurationId;
    private readonly Percentage vatRate;
    private readonly TimeProvider timeProvider;

    public CommissionService(
        ICommissionBindingRepository bindingRepository,
        ICommissionConfigurationRepository configurationRepository,
        CommissionCalculator calculator,
        IOptions<PlatformCommissionOptions> options,
        IOptions<PlatformCommissionTaxOptions> taxOptions,
        TimeProvider timeProvider)
    {
        this.bindingRepository = bindingRepository;
        this.configurationRepository = configurationRepository;
        this.calculator = calculator;
        this.currentConfigurationId = options.Value.ConfigurationId;
        this.vatRate = Percentage.From(taxOptions.Value.VatRatePercentage);
        this.timeProvider = timeProvider;
    }

    public async Task<Result<Concertable.Payment.Contracts.CommissionCalculation>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default)
    {
        if (currency != Currency.Gbp)
            return Result.Fail("currency_mismatch");

        var terms = (await GetCurrentConfigurationAsync(ct)).Terms;
        return Result.Ok(ToCalculation(terms, Calculate(terms, grossMinor, currency)));
    }

    public async Task<Result<CommissionBinding>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor,
        long? expectedCommissionMinor,
        long? expectedPayerTotalMinor,
        CancellationToken ct = default)
    {
        if (reviewedCommissionConfigurationId != currentConfigurationId)
            return Result.Fail("pricing_changed");
        if (currency != Currency.Gbp)
            return Result.Fail("currency_mismatch");

        var configuration = await GetCurrentConfigurationAsync(ct);
        var terms = configuration.Terms;
        var validation = ValidateExpected(
            terms,
            currency,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor);
        if (validation.IsFailed)
            return validation.ToResult<CommissionBinding>();

        var binding = await bindingRepository.GetOrCreateAsync(
            CommissionBindingEntity.Create(
                configuration,
                currency,
                externalReference,
                payerReference,
                timeProvider.GetUtcNow(),
                stripePaymentIntentId,
                stripeSetupIntentId),
            ct);

        return ExistingBinding(
            binding,
            terms,
            currency,
            externalReference,
            payerReference,
            stripePaymentIntentId,
            stripeSetupIntentId,
            grossMinor);
    }

    public async Task<Result<BoundCommission>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var binding = await bindingRepository.GetByIdAsync(bindingId, ct);
        if (binding is null)
            return Result.Fail("commission_binding_not_found");

        if (!string.Equals(binding.ExternalReference, externalReference, StringComparison.Ordinal) ||
            !string.Equals(binding.PayerReference, payerReference, StringComparison.Ordinal))
            return Result.Fail("commission_binding_mismatch");
        if (currency != binding.Currency)
            return Result.Fail("currency_mismatch");
        if (!IntentMatches(binding.StripePaymentIntentId, stripePaymentIntentId) ||
            !IntentMatches(binding.StripeSetupIntentId, stripeSetupIntentId))
            return Result.Fail("commission_binding_intent_mismatch");

        var terms = binding.Terms;
        return Result.Ok(new BoundCommission(
            binding,
            terms,
            Calculate(terms, grossMinor, binding.Currency)));
    }

    public async Task<string?> FindBoundPaymentIntentAsync(
        Guid bindingId,
        CancellationToken ct = default)
    {
        var binding = await bindingRepository.GetByIdAsync(bindingId, ct);
        return binding?.StripePaymentIntentId;
    }

    public void BindPaymentIntent(
        CommissionBindingEntity binding,
        string paymentIntentId) =>
        binding.BindPaymentIntent(paymentIntentId);

    private async Task<CommissionConfigurationEntity> GetCurrentConfigurationAsync(
        CancellationToken ct)
    {
        var configuration = await configurationRepository.GetByIdAsync(currentConfigurationId, ct);
        return configuration ?? throw new InvalidOperationException(
            "Configured commission revision has not been initialized.");
    }

    private Result ValidateExpected(
        CommissionTerms terms,
        Currency currency,
        long? grossMinor,
        long? expectedCommissionMinor,
        long? expectedPayerTotalMinor)
    {
        if (grossMinor is null)
            return expectedCommissionMinor is null && expectedPayerTotalMinor is null
                ? Result.Ok()
                : Result.Fail("Expected amounts require a gross amount.");

        if (expectedCommissionMinor is null || expectedPayerTotalMinor is null)
            return Result.Fail("Expected commission and payer total are required when gross is supplied.");

        var calculation = Calculate(terms, grossMinor.Value, currency);
        return calculation.CommissionGrossMinor == expectedCommissionMinor.Value &&
               calculation.PayerTotalMinor == expectedPayerTotalMinor.Value
            ? Result.Ok()
            : Result.Fail("pricing_changed");
    }

    private Result<CommissionBinding> ExistingBinding(
        CommissionBindingEntity binding,
        CommissionTerms currentTerms,
        Currency currency,
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor)
    {
        if (!binding.Matches(
                currentTerms.ConfigurationId,
                currency,
                externalReference,
                payerReference,
                stripePaymentIntentId,
                stripeSetupIntentId))
            return Result.Fail("commission_binding_mismatch");

        var terms = binding.Terms;
        return Result.Ok(ToBinding(
            binding,
            terms,
            grossMinor is null ? null : Calculate(terms, grossMinor.Value, binding.Currency)));
    }

    private Concertable.Payment.Domain.CommissionCalculation Calculate(
        CommissionTerms terms,
        long grossMinor,
        Currency currency) =>
        calculator.Calculate(
            grossMinor,
            currency,
            terms,
            vatRate);

    private static bool IntentMatches(string? bound, string? supplied) =>
        bound is null || string.Equals(bound, supplied, StringComparison.Ordinal);

    private static CommissionBinding ToBinding(
        CommissionBindingEntity binding,
        CommissionTerms terms,
        Concertable.Payment.Domain.CommissionCalculation? calculation) =>
        new(
            binding.Id,
            terms.ConfigurationId,
            terms.Rate.Value,
            binding.Currency,
            calculation is null ? null : ToCalculation(terms, calculation.Value));

    private static Concertable.Payment.Contracts.CommissionCalculation ToCalculation(
        CommissionTerms terms,
        Concertable.Payment.Domain.CommissionCalculation calculation) =>
        new(
            terms.ConfigurationId,
            terms.Rate.Value,
            calculation.Currency,
            calculation.PayeeGrossMinor,
            calculation.CommissionGrossMinor,
            calculation.PayerTotalMinor);
}
