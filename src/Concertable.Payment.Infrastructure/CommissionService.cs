using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionService : ICommissionService
{
    private readonly ICommissionBindingRepository bindingRepository;
    private readonly CommissionCalculator calculator;
    private readonly PlatformCommissionOptions options;
    private readonly PlatformCommissionTaxOptions taxOptions;
    private readonly TimeProvider timeProvider;

    public CommissionService(
        ICommissionBindingRepository bindingRepository,
        CommissionCalculator calculator,
        IOptions<PlatformCommissionOptions> options,
        IOptions<PlatformCommissionTaxOptions> taxOptions,
        TimeProvider timeProvider)
    {
        this.bindingRepository = bindingRepository;
        this.calculator = calculator;
        this.options = options.Value;
        this.taxOptions = taxOptions.Value;
        this.timeProvider = timeProvider;
    }

    public Task<Result<CommissionQuote, CommissionError>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default)
    {
        var terms = CurrentTerms();
        return Task.FromResult(currency != terms.Currency
            ? Result.Failure<CommissionQuote, CommissionError>(CommissionError.CurrencyMismatch)
            : Result.Success<CommissionQuote, CommissionError>(ToQuote(terms, Calculate(terms, grossMinor))));
    }

    public async Task<Result<CommissionBinding, CommissionError>> CreateOrBindAsync(
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
        if (reviewedCommissionConfigurationId != options.ConfigurationId)
            return Result.Failure<CommissionBinding, CommissionError>(CommissionError.PricingChanged);

        var terms = CurrentTerms();
        if (currency != terms.Currency)
            return Result.Failure<CommissionBinding, CommissionError>(CommissionError.CurrencyMismatch);

        var validation = ValidateExpected(
            terms,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor);
        if (validation.TryGetError(out var validationError))
            return Result.Failure<CommissionBinding, CommissionError>(validationError);

        var binding = await bindingRepository.GetOrCreateAsync(
            CommissionBindingEntity.Create(
                terms,
                externalReference,
                payerReference,
                timeProvider.GetUtcNow(),
                stripePaymentIntentId,
                stripeSetupIntentId),
            ct);

        return ExistingBinding(
            binding,
            terms,
            externalReference,
            payerReference,
            stripePaymentIntentId,
            stripeSetupIntentId,
            grossMinor);
    }

    public async Task<Result<BoundCommission, CommissionError>> CalculateBoundAsync(
        Guid bindingId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var binding = await bindingRepository.GetByIdAsync(bindingId, ct);
        if (binding is null)
            return Result.Failure<BoundCommission, CommissionError>(CommissionError.BindingNotFound);

        if (!string.Equals(binding.ExternalReference, externalReference, StringComparison.Ordinal) ||
            !string.Equals(binding.PayerReference, payerReference, StringComparison.Ordinal))
            return Result.Failure<BoundCommission, CommissionError>(CommissionError.BindingMismatch);

        var terms = binding.Terms;
        if (currency != terms.Currency)
            return Result.Failure<BoundCommission, CommissionError>(CommissionError.CurrencyMismatch);
        if (!IntentMatches(binding.StripePaymentIntentId, stripePaymentIntentId) ||
            !IntentMatches(binding.StripeSetupIntentId, stripeSetupIntentId))
            return Result.Failure<BoundCommission, CommissionError>(CommissionError.BindingIntentMismatch);

        var calculation = Calculate(terms, grossMinor);
        if (calculation.CommissionGrossMinor != expectedCommissionMinor ||
            calculation.PayerTotalMinor != expectedPayerTotalMinor)
            return Result.Failure<BoundCommission, CommissionError>(CommissionError.PricingChanged);

        return Result.Success<BoundCommission, CommissionError>(new BoundCommission(binding, terms, calculation));
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

    private CommissionTerms CurrentTerms() =>
        new(
            options.ConfigurationId,
            options.Version,
            Enum.Parse<Currency>(options.Currency, ignoreCase: true),
            options.RateBasisPoints,
            taxOptions.VatRateBasisPoints);

    private UnitResult<CommissionError> ValidateExpected(
        CommissionTerms terms,
        long? grossMinor,
        long? expectedCommissionMinor,
        long? expectedPayerTotalMinor)
    {
        if (grossMinor is null)
            return expectedCommissionMinor is null && expectedPayerTotalMinor is null
                ? UnitResult.Success<CommissionError>()
                : UnitResult.Failure(CommissionError.ExpectedAmountsInvalid);

        if (expectedCommissionMinor is null || expectedPayerTotalMinor is null)
            return UnitResult.Failure(CommissionError.ExpectedAmountsInvalid);

        var calculation = Calculate(terms, grossMinor.Value);
        return calculation.CommissionGrossMinor == expectedCommissionMinor.Value &&
               calculation.PayerTotalMinor == expectedPayerTotalMinor.Value
            ? UnitResult.Success<CommissionError>()
            : UnitResult.Failure(CommissionError.PricingChanged);
    }

    private Result<CommissionBinding, CommissionError> ExistingBinding(
        CommissionBindingEntity binding,
        CommissionTerms currentTerms,
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor)
    {
        if (!binding.Matches(
                currentTerms.ConfigurationId,
                externalReference,
                payerReference,
                stripePaymentIntentId,
                stripeSetupIntentId))
            return Result.Failure<CommissionBinding, CommissionError>(CommissionError.BindingMismatch);

        var terms = binding.Terms;
        return Result.Success<CommissionBinding, CommissionError>(ToBinding(
            binding,
            terms,
            grossMinor is null ? null : Calculate(terms, grossMinor.Value)));
    }

    private CommissionCalculation Calculate(
        CommissionTerms terms,
        long grossMinor) =>
        calculator.Calculate(
            grossMinor,
            terms.Currency,
            terms.RateBasisPoints,
            terms.VatRateBasisPoints);

    private static bool IntentMatches(string? bound, string? supplied) =>
        bound is null || string.Equals(bound, supplied, StringComparison.Ordinal);

    private static CommissionBinding ToBinding(
        CommissionBindingEntity binding,
        CommissionTerms terms,
        CommissionCalculation? calculation) =>
        new(
            binding.Id,
            terms.ConfigurationId,
            terms.Version,
            terms.RateBasisPoints,
            terms.Currency,
            calculation is null ? null : ToQuote(terms, calculation.Value));

    private static CommissionQuote ToQuote(
        CommissionTerms terms,
        CommissionCalculation calculation) =>
        new(
            terms.ConfigurationId,
            terms.Version,
            terms.RateBasisPoints,
            terms.Currency,
            calculation.PayeeGrossMinor,
            calculation.CommissionGrossMinor,
            calculation.PayerTotalMinor);
}
