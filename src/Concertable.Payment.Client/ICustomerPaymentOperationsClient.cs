using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Client;

public interface ICustomerPaymentOperationsClient
{
    Task<Result<PaymentOutcome, PaymentError>> PayAsync(
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
