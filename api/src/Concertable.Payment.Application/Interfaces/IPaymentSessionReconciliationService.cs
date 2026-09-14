using Concertable.Payment.Application.PaymentSessions;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionReconciliationService
{
    Task<Result<PaymentSessionReconciliation, PaymentOperationError.ProviderUnavailable>> ReconcileAsync(
        PaymentSessionReconciliationRequest request,
        CancellationToken ct = default);
}
