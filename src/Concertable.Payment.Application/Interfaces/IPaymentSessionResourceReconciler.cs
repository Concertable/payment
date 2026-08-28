using Concertable.Payment.Application.PaymentSessions;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionResourceReconciler
{
    Task ReconcileByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        PaymentSessionReconciliationSource source,
        PaymentSessionProviderEventEvidence? eventEvidence,
        CancellationToken ct = default);
}
