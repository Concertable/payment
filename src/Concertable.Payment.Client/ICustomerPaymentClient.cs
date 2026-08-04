using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Functional = Concertable.Kernel.Functional;

namespace Concertable.Payment.Client;

public interface ICustomerPaymentClient
{
    Task<Functional.Result<PaymentOutcome, PaymentError>> PurchaseAsync(
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
