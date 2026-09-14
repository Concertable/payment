using Concertable.Kernel;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Domain.Events;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class PaymentOperationStateChangedDomainEventHandler
    : IPreCommitDomainEventHandler<PaymentOperationStateChangedDomainEvent>
{
    private readonly IBus bus;

    public PaymentOperationStateChangedDomainEventHandler(IBus bus)
    {
        this.bus = bus;
    }

    public Task HandleAsync(PaymentOperationStateChangedDomainEvent e, CancellationToken ct = default) =>
        bus.PublishAsync(
            new PaymentOperationStateChanged(
                e.Identity,
                e.SessionKind,
                e.State,
                e.TerminalDisposition,
                e.RetryDisposition,
                e.Failure,
                e.ExpiresAt,
                e.CaptureBefore,
                e.ObservedAt),
            ct);
}
