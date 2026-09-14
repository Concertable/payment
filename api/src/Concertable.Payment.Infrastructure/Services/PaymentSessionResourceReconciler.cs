using Concertable.Payment.Application.PaymentSessions;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentSessionResourceReconciler : IPaymentSessionResourceReconciler
{
    private readonly IStripeSessionClient stripeSessionClient;
    private readonly IPaymentSessionOperationRepository operationRepository;
    private readonly IPaymentSessionReconciliationService reconciliationService;
    private readonly ILogger<PaymentSessionResourceReconciler> logger;

    public PaymentSessionResourceReconciler(
        IStripeSessionClient stripeSessionClient,
        IPaymentSessionOperationRepository operationRepository,
        IPaymentSessionReconciliationService reconciliationService,
        ILogger<PaymentSessionResourceReconciler> logger)
    {
        this.stripeSessionClient = stripeSessionClient;
        this.operationRepository = operationRepository;
        this.reconciliationService = reconciliationService;
        this.logger = logger;
    }

    public async Task ReconcileByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        PaymentSessionReconciliationSource source,
        PaymentSessionProviderEventEvidence? eventEvidence,
        CancellationToken ct = default)
    {
        var operation = await operationRepository.GetByProviderObjectAsync(
            providerObjectKind,
            providerObjectId,
            ct);
        var attempt = operation?.Attempts.SingleOrDefault(candidate =>
            candidate.ProviderObjectKind == providerObjectKind
            && string.Equals(candidate.ProviderObjectId, providerObjectId, StringComparison.Ordinal));
        if (operation is null || attempt is null)
        {
            logger.SkippingUntrackedSessionResource(providerObjectId, providerObjectKind, source);
            return;
        }

        logger.ReconcilingSessionResource(providerObjectId, providerObjectKind, source);

        var retrieved = await stripeSessionClient.RetrieveAsync(providerObjectKind, providerObjectId, ct);
        var provider = retrieved.TryGetValue(out var result) ? result : null;

        var reconciled = await reconciliationService.ReconcileAsync(
            new PaymentSessionReconciliationRequest(operation, attempt, source, provider, eventEvidence),
            ct);

        if (!reconciled.TryGetValue(out _))
            logger.SessionResourceReconciliationDeferred(providerObjectId, providerObjectKind, source);
    }
}
