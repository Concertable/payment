using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICustomerPaymentService
{
    Task<Result<PaymentOutcome>> PayAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default);

    Task<CheckoutSession> CreatePaymentSessionAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);
}
