using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionService
{
    Task<Result<PaymentSessionExecution, PaymentOperationError>> SetupPaymentMethodAsync(
        PaymentMethodSetupRequest request,
        CancellationToken ct = default);

    Task<UnitResult<PaymentOperationError>> ValidatePaymentMethodAsync(
        PaymentMethodValidationRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentSessionExecution, PaymentOperationError>> CreateOrReplayAsync(
        PaymentSessionOperationRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentSessionExecution, PaymentOperationError>> RetryAsync(
        PaymentSessionRetryRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentSessionStatus, PaymentOperationError>> RefreshAsync(
        PaymentSessionStatusRequest request,
        CancellationToken ct = default);
}
