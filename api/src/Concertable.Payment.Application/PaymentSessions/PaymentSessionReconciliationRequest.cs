using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionReconciliationRequest(
    PaymentSessionOperationEntity Operation,
    PaymentSessionAttemptEntity Attempt,
    PaymentSessionReconciliationSource Source,
    ProviderSession? Provider,
    PaymentSessionProviderEventEvidence? EventEvidence = null);
