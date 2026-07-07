using Concertable.Payment.Contracts;
using FluentResults;

namespace Concertable.Payment.Client;

public interface ICustomerPaymentClient
{
    Task<Result<PaymentOutcome>> PayAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        decimal amount,
        IDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default);

    Task<CheckoutSession> CreatePaymentSessionAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);
}
