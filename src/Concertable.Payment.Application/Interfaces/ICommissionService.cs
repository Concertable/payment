using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionService
{
    Task<Result<CommissionQuote>> PreviewAsync(
        long grossMinor,
        Currency currency,
        CancellationToken ct = default);

    Task<Result<CommissionAuthorization>> CreateOrBindAuthorizationAsync(
        string externalReference,
        string payerReference,
        Currency currency,
        Guid reviewedCommissionConfigurationId,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        long? grossMinor,
        long? expectedCommissionMinor,
        long? expectedPayerTotalMinor,
        CancellationToken ct = default);

    Task<Result<AuthorizedCommission>> CalculateAuthorizedAsync(
        Guid authorizationId,
        string externalReference,
        string payerReference,
        Currency currency,
        long grossMinor,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<Result> ClaimAuthorizationAsync(
        Guid authorizationId,
        CommissionAuthorizationConsumer consumer,
        CancellationToken ct = default);

    Task<string?> FindBoundPaymentIntentAsync(
        Guid authorizationId,
        CancellationToken ct = default);

    void BindPaymentIntent(
        CommissionAuthorizationEntity authorization,
        string paymentIntentId);
}
