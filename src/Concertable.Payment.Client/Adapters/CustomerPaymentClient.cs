using Concertable.Kernel.Functional;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class CustomerPaymentClient : ICustomerPaymentOperationsClient
{
    private readonly Proto.CustomerPayment.CustomerPaymentClient client;

    public CustomerPaymentClient(Proto.CustomerPayment.CustomerPaymentClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentOutcome, PaymentError>> PayAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        decimal amount,
        IReadOnlyDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var request = new Proto.CustomerPayRequest
                {
                    PayerId = payerId.ToString(),
                    ConcertId = concertId,
                    PayeeId = payeeId.ToString(),
                    Amount = Money.Gbp(amount).ToProtoMoney(),
                    PaymentMethodId = paymentMethodId
                };
                request.Metadata.Add(new Dictionary<string, string>(metadata));
                return (await client.PayAsync(request, cancellationToken: ct)).ToPaymentOutcome();
            },
            PaymentError.FromCode,
            ct);

    public async Task<CheckoutSession> CreatePaymentSessionAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var request = new Proto.CreatePaymentSessionRequest
        {
            PayerId = payerId.ToString(),
            ConcertId = concertId,
            PayeeId = payeeId.ToString()
        };
        request.Metadata.Add(new Dictionary<string, string>(metadata));
        return (await client.CreatePaymentSessionAsync(request, cancellationToken: ct)).ToCheckoutSession();
    }

}
