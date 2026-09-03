using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Reunion;

namespace Concertable.Payment.Client;

public interface IPaymentSessionOperationsClient
{
    Task<Result<PaymentMethodSetup, PaymentOperationError>> SetupPaymentMethodAsync(
        PaymentMethodSetupRequest request,
        CancellationToken ct = default);

    Task<UnitResult<PaymentOperationError>> ValidatePaymentMethodAsync(
        PaymentMethodValidationRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentSessionDescriptor, PaymentOperationError>> CreateOrReplayAsync(
        PaymentSessionOperationRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentSessionDescriptor, PaymentOperationError>> RetryAsync(
        PaymentSessionRetryRequest request,
        CancellationToken ct = default);

    Task<Result<PaymentOperationSnapshot, PaymentOperationError>> GetStatusAsync(
        PaymentSessionStatusRequest request,
        CancellationToken ct = default);
}
