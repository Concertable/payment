using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionReconciliation(
    PaymentSessionAttemptEntity Attempt,
    Result<PaymentOperationTransition, PaymentOperationTransitionRejection> Evaluation);
