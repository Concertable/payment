using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Client;

public interface IManagerPaymentOperationsClient
{
    Task<Result<PaymentOutcome, ManagerPaymentError>> PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, ManagerPaymentError>> PayBoundCommissionAsync(
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

    Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        decimal amount,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<Result<CheckoutSession, HoldSessionError>> CreateBoundCommissionHoldSessionAsync(
        Guid payerId,
        long grossMinor,
        Currency currency,
        IDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default);

    Task<string> FindHeldIntentAsync(
        Guid payerId,
        int applicationId,
        CancellationToken ct = default);
}
