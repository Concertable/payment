using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Settings;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionService : ICommissionService
{
    private readonly ICommissionConfigurationRepository configurationRepository;
    private readonly ICommissionAuthorizationRepository authorizationRepository;
    private readonly ICommissionAuthorizationClaimRepository claimRepository;
    private readonly PaymentDbContext context;
    private readonly IUnitOfWork unitOfWork;
    private readonly CommissionCalculator calculator;
    private readonly PlatformCommissionOptions options;
    private readonly PlatformCommissionTaxOptions taxOptions;
    private readonly TimeProvider timeProvider;

    public CommissionService(
        ICommissionConfigurationRepository configurationRepository,
        ICommissionAuthorizationRepository authorizationRepository,
        ICommissionAuthorizationClaimRepository claimRepository,
        PaymentDbContext context,
        IUnitOfWork unitOfWork,
        CommissionCalculator calculator,
        IOptions<PlatformCommissionOptions> options,
        IOptions<PlatformCommissionTaxOptions> taxOptions,
        TimeProvider timeProvider)
    {
        this.configurationRepository = configurationRepository;
        this.authorizationRepository = authorizationRepository;
        this.claimRepository = claimRepository;
        this.context = context;
        this.unitOfWork = unitOfWork;
        this.calculator = calculator;
        this.options = options.Value;
        this.taxOptions = taxOptions.Value;
        this.timeProvider = timeProvider;
    }

    public async Task<Result<CommissionQuote>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default)
    {
        var configuration = await GetCurrentConfigurationAsync(ct);
        if (currency != configuration.Currency)
            return Result.Fail("currency_mismatch");

        return Result.Ok(ToQuote(configuration, Calculate(configuration, grossMinor)));
    }

    public async Task<Result<CommissionAuthorization>> CreateOrBindAuthorizationAsync(
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
            return Result.Fail("pricing_changed");

        var configuration = await GetCurrentConfigurationAsync(ct);
        if (currency != configuration.Currency)
            return Result.Fail("currency_mismatch");

        var validation = ValidateExpected(
            configuration,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor);
        if (validation.IsFailed)
            return validation.ToResult<CommissionAuthorization>();

        var existing = await authorizationRepository.GetByIdentityAsync(
            externalReference,
            payerReference,
            ct);
        if (existing is not null)
            return ExistingAuthorization(
                existing,
                configuration,
                externalReference,
                payerReference,
                stripePaymentIntentId,
                stripeSetupIntentId,
                grossMinor);

        var authorization = CommissionAuthorizationEntity.Create(
            configuration.Id,
            externalReference,
            payerReference,
            timeProvider.GetUtcNow(),
            stripePaymentIntentId,
            stripeSetupIntentId);
        await authorizationRepository.AddAsync(authorization, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            existing = await authorizationRepository.GetByIdentityAsync(
                externalReference,
                payerReference,
                ct);
            if (existing is null)
                throw;

            return ExistingAuthorization(
                existing,
                configuration,
                externalReference,
                payerReference,
                stripePaymentIntentId,
                stripeSetupIntentId,
                grossMinor);
        }

        return Result.Ok(ToAuthorization(
            authorization,
            configuration,
            grossMinor is null ? null : Calculate(configuration, grossMinor.Value)));
    }

    public async Task<Result<AuthorizedCommission>> CalculateAuthorizedAsync(
        Guid authorizationId,
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
        var authorization = await authorizationRepository.GetByIdAsync(authorizationId, ct);
        if (authorization is null)
            return Result.Fail("commission_authorization_not_found");

        if (!string.Equals(authorization.ExternalReference, externalReference, StringComparison.Ordinal) ||
            !string.Equals(authorization.PayerReference, payerReference, StringComparison.Ordinal))
            return Result.Fail("commission_authorization_mismatch");

        var configuration = authorization.CommissionConfiguration;
        if (currency != configuration.Currency)
            return Result.Fail("currency_mismatch");
        if (!IntentMatches(authorization.StripePaymentIntentId, stripePaymentIntentId) ||
            !IntentMatches(authorization.StripeSetupIntentId, stripeSetupIntentId))
            return Result.Fail("commission_authorization_intent_mismatch");

        var calculation = Calculate(configuration, grossMinor);
        if (calculation.CommissionGrossMinor != expectedCommissionMinor ||
            calculation.PayerTotalMinor != expectedPayerTotalMinor)
            return Result.Fail("pricing_changed");

        return Result.Ok(new AuthorizedCommission(authorization, configuration, calculation));
    }

    public async Task<Result> ClaimAuthorizationAsync(
        Guid authorizationId,
        CommissionAuthorizationConsumer consumer,
        CancellationToken ct = default)
    {
        var claim = CommissionAuthorizationClaimEntity.Create(
            authorizationId,
            consumer,
            timeProvider.GetUtcNow());
        await claimRepository.AddAsync(claim, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            context.Entry(claim).State = EntityState.Detached;
            var existing = await claimRepository.GetByCommissionAuthorizationIdAsync(authorizationId, ct);
            if (existing is null)
                throw;

            return existing.Consumer == consumer
                ? Result.Ok()
                : Result.Fail("commission_authorization_already_consumed");
        }

        return Result.Ok();
    }

    public void BindPaymentIntent(
        CommissionAuthorizationEntity authorization,
        string paymentIntentId) =>
        authorization.BindPaymentIntent(paymentIntentId);

    private async Task<CommissionConfigurationEntity> GetCurrentConfigurationAsync(CancellationToken ct)
    {
        var configuration = await configurationRepository.GetByIdAsync(options.ConfigurationId, ct);
        return configuration is not null &&
               configuration.HasTerms(
                   options.ConfigurationId,
                   options.Version,
                   Enum.Parse<Currency>(options.Currency, ignoreCase: true),
                   options.RateBasisPoints)
            ? configuration
            : throw new InvalidOperationException("Configured commission revision is missing or does not match immutable terms.");
    }

    private Result ValidateExpected(
        CommissionConfigurationEntity configuration,
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

        var calculation = Calculate(configuration, grossMinor.Value);
        return calculation.CommissionGrossMinor == expectedCommissionMinor.Value &&
               calculation.PayerTotalMinor == expectedPayerTotalMinor.Value
            ? Result.Ok()
            : Result.Fail("pricing_changed");
    }

    private Result<CommissionAuthorization> ExistingAuthorization(
        CommissionAuthorizationEntity authorization,
        CommissionConfigurationEntity configuration,
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor)
    {
        if (!authorization.Matches(
                configuration.Id,
                externalReference,
                payerReference,
                stripePaymentIntentId,
                stripeSetupIntentId))
            return Result.Fail("commission_authorization_mismatch");

        return Result.Ok(ToAuthorization(
            authorization,
            configuration,
            grossMinor is null ? null : Calculate(configuration, grossMinor.Value)));
    }

    private CommissionCalculation Calculate(
        CommissionConfigurationEntity configuration,
        long grossMinor) =>
        calculator.Calculate(
            grossMinor,
            configuration.Currency,
            configuration.RateBasisPoints,
            taxOptions.VatRateBasisPoints);

    private static bool IntentMatches(string? bound, string? supplied) =>
        bound is null || string.Equals(bound, supplied, StringComparison.Ordinal);

    private static CommissionAuthorization ToAuthorization(
        CommissionAuthorizationEntity authorization,
        CommissionConfigurationEntity configuration,
        CommissionCalculation? calculation) =>
        new(
            authorization.Id,
            configuration.Id,
            configuration.Version,
            configuration.RateBasisPoints,
            configuration.Currency,
            calculation is null ? null : ToQuote(configuration, calculation.Value));

    private static CommissionQuote ToQuote(
        CommissionConfigurationEntity configuration,
        CommissionCalculation calculation) =>
        new(
            configuration.Id,
            configuration.Version,
            configuration.RateBasisPoints,
            configuration.Currency,
            calculation.PayeeGrossMinor,
            calculation.CommissionGrossMinor,
            calculation.PayerTotalMinor);
}
