using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.PublishedContractFixture;

public static class PublishedContractConsumer
{
    public static object[] CreateContracts(Guid operationId, Guid payerId, Guid payeeId, Money money) =>
    [
        new CheckoutSession("secret", "customer-session", "customer"),
        new PaymentOutcome { ClientSecret = "secret", TransactionId = "transaction", RequiresAction = true },
        new EscrowDeposit(1, "charge", EscrowStatus.Held),
        new Transfer("transfer"),
        new Refund("refund"),
        new PaymentSucceededEvent("transaction", new Dictionary<string, string>()),
        new PaymentFailedEvent("transaction", "code", "message", new Dictionary<string, string>()),
        new PayoutOwnerRegisteredEvent(payerId, "buyer@example.com"),
        new CaptureEscrowCommand(operationId, 1, payerId, payeeId, money.ToMinorUnits(), money.Currency, "payment-intent"),
        new DepositEscrowCommand(operationId, 1, payerId, payeeId, money.ToMinorUnits(), money.Currency, "payment-method", PaymentSession.OnSession),
        new RefundEscrowCommand(operationId, 1)
    ];

    public static Task[] CallClients(
        ICustomerPaymentOperationsClient customer,
        IManagerPaymentOperationsClient manager,
        IEscrowOperationsClient escrow,
        IPayoutAccountOperationsClient payout,
        Guid payerId,
        Guid payeeId,
        Money money,
        CancellationToken cancellationToken) =>
    [
        customer.CreatePaymentSessionAsync(payerId, 1, payeeId, new Dictionary<string, string>(), cancellationToken),
        customer.PayAsync(payerId, 1, payeeId, money, new Dictionary<string, string>(), "payment-method", cancellationToken),
        manager.CreateSetupSessionAsync(payerId, new Dictionary<string, string>(), cancellationToken),
        manager.CreateVerifySessionAsync(payerId, new Dictionary<string, string>(), cancellationToken),
        manager.CreateHoldSessionAsync(payerId, money, new Dictionary<string, string>(), cancellationToken),
        escrow.CaptureAsync(payerId, payeeId, money, "payment-intent", 1, cancellationToken),
        escrow.ReleaseByBookingIdAsync(1, cancellationToken),
        escrow.RefundByBookingIdAsync(1, cancellationToken),
        payout.GetAccountStatusAsync(payerId, cancellationToken),
        payout.GetPaymentMethodAsync(payerId, cancellationToken)
    ];
}
