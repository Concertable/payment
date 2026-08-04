using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Functional = Concertable.Kernel.Functional;

namespace Concertable.Payment.Client;

public interface IEscrowClient
{
    Task<Functional.Result<EscrowDeposit, DepositError>> CreateDepositAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Functional.Result<EscrowDeposit, DepositError>> CreateBoundCommissionDepositAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default);

    Task<Functional.Result<EscrowDeposit, CaptureError>> CaptureDepositAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default);

    Task<Functional.Result<EscrowDeposit, CaptureError>> CaptureBoundCommissionDepositAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Functional.Result<Functional.Option<Transfer>, ReleaseError>> ReleaseAsync(
        int bookingId,
        CancellationToken ct = default);

    Task<Functional.Result<Functional.Option<Refund>, RefundError>> RefundAsync(
        int bookingId,
        CancellationToken ct = default);

    Task<Functional.Result<Functional.Option<Refund>, RefundError>> RefundBoundCommissionAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        CancellationToken ct = default);
}
