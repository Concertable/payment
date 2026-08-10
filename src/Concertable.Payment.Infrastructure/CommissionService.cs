using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Reunion;
using Concertable.Payment.Contracts.Errors;
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

    public async Task<Result<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>> PreviewAsync(
        Money gross,
        CancellationToken ct = default)
    {
        if (gross.Currency != Currency.Gbp)
            return Result.Failure<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
                new CommissionError.CurrencyMismatch());

        var terms = (await GetCurrentConfigurationAsync(ct)).Terms;
        return Result.Success<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
            ToCalculation(terms, Calculate(terms, gross)));
    }

    public async Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        if (reviewedCommissionConfigurationId != currentConfigurationId)
            return Result.Failure<CommissionBinding, CommissionError>(new CommissionError.PricingChanged());
        if (currency != Currency.Gbp)
            return Result.Failure<CommissionBinding, CommissionError>(new CommissionError.CurrencyMismatch());

        var configuration = await GetCurrentConfigurationAsync(ct);
        var terms = configuration.Terms;
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
            stripeSetupIntentId);
    }

    public async Task<Result<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>> ConfirmReviewedGrossAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money reviewedGross,
        CancellationToken ct = default)
    {
        var binding = await bindingRepository.GetByIdAsync(bindingId, ct);
        if (binding is null)
            return Result.Failure<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
                new CommissionError.BindingNotFound());
        if (!IdentityMatches(binding, externalReference, payerReference))
            return Result.Failure<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
                new CommissionError.BindingMismatch());
        if (reviewedGross.Currency != binding.Currency)
            return Result.Failure<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
                new CommissionError.CurrencyMismatch());
        var existing = binding.ReviewedGross;
        if (existing is not null && existing.Value != reviewedGross)
            return Result.Failure<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
                new CommissionError.GrossMismatch());

        var calculation = Calculate(binding.Terms, reviewedGross);
        if (!await bindingRepository.TryConfirmReviewedGrossAsync(bindingId, reviewedGross, ct))
            return Result.Failure<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
                new CommissionError.GrossMismatch());

        binding.ConfirmReviewedGross(reviewedGross);

        return Result.Success<Concertable.Payment.Contracts.CommissionCalculation, CommissionError>(
            ToCalculation(binding.Terms, calculation));
    }

    public async Task<Result<BoundCommission, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Money gross,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var binding = await bindingRepository.GetByIdAsync(bindingId, ct);
        if (binding is null)
            return Result.Failure<BoundCommission, CommissionError>(new CommissionError.BindingNotFound());

        if (!IdentityMatches(binding, externalReference, payerReference))
            return Result.Failure<BoundCommission, CommissionError>(new CommissionError.BindingMismatch());

        if (gross.Currency != binding.Currency)
            return Result.Failure<BoundCommission, CommissionError>(new CommissionError.CurrencyMismatch());
        if (binding.ReviewedGross is null)
            return Result.Failure<BoundCommission, CommissionError>(new CommissionError.GrossNotConfirmed());
        if (binding.ReviewedGross != gross)
            return Result.Failure<BoundCommission, CommissionError>(new CommissionError.GrossMismatch());
        if (!IntentMatches(binding.StripePaymentIntentId, stripePaymentIntentId) ||
            !IntentMatches(binding.StripeSetupIntentId, stripeSetupIntentId))
            return Result.Failure<BoundCommission, CommissionError>(new CommissionError.BindingIntentMismatch());

        var terms = binding.Terms;
        return Result.Success<BoundCommission, CommissionError>(new BoundCommission(
            binding,
            terms,
            Calculate(terms, gross)));
    }

    public async Task<Option<string>> FindBoundPaymentIntentAsync(
        Guid bindingId,
        CancellationToken ct = default)
    {
        var binding = await bindingRepository.GetByIdAsync(bindingId, ct);
        return Option.FromNullable(binding?.StripePaymentIntentId);
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

    private Result<CommissionBinding, CommissionError> ExistingBinding(
        CommissionBindingEntity binding,
        CommissionTerms currentTerms,
        Currency currency,
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId)
    {
        if (!binding.Matches(
                currentTerms.ConfigurationId,
                currency,
                externalReference,
                payerReference,
                stripePaymentIntentId,
                stripeSetupIntentId))
            return Result.Failure<CommissionBinding, CommissionError>(new CommissionError.BindingMismatch());

        return Result.Success<CommissionBinding, CommissionError>(ToBinding(binding, binding.Terms));
    }

    private Concertable.Payment.Domain.CommissionCalculation Calculate(
        CommissionTerms terms,
        Money gross) =>
        calculator.Calculate(
            gross.ToMinorUnits(),
            gross.Currency,
            terms,
            vatRate);

    private static bool IdentityMatches(
        CommissionBindingEntity binding,
        string externalReference,
        string payerReference) =>
        string.Equals(binding.ExternalReference, externalReference, StringComparison.Ordinal) &&
        string.Equals(binding.PayerReference, payerReference, StringComparison.Ordinal);

    private static bool IntentMatches(string? bound, string? supplied) =>
        bound is null || string.Equals(bound, supplied, StringComparison.Ordinal);

    private static CommissionBinding ToBinding(
        CommissionBindingEntity binding,
        CommissionTerms terms) =>
        new(
            binding.Id,
            terms.ConfigurationId,
            terms.Rate.Value,
            binding.Currency);

    private static Concertable.Payment.Contracts.CommissionCalculation ToCalculation(
        CommissionTerms terms,
        Concertable.Payment.Domain.CommissionCalculation calculation) =>
        new(
            terms.ConfigurationId,
            terms.Rate.Value,
            Money.FromMinorUnits(calculation.PayeeGrossMinor, calculation.Currency),
            Money.FromMinorUnits(calculation.CommissionGrossMinor, calculation.Currency),
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency));
}
