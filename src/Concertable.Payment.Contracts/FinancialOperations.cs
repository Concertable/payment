using Concertable.Kernel.ValueObjects;
using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts;

[MessageType("concertable.payment.capture-escrow.v1")]
public sealed record CaptureEscrowCommand(
    Guid OperationId,
    PaymentOperationReference Reference,
    Guid PayerId,
    Guid PayeeId,
    long AmountMinor,
    Currency Currency,
    PaymentOperationReference Authorization) : IIntegrationCommand;

[MessageType("concertable.payment.deposit-escrow.v1")]
public sealed record DepositEscrowCommand(
    Guid OperationId,
    PaymentOperationReference Reference,
    Guid PayerId,
    Guid PayeeId,
    long AmountMinor,
    Currency Currency,
    PaymentOperationReference PaymentMethod,
    PaymentSession Session) : IIntegrationCommand;

[MessageType("concertable.payment.refund-escrow.v1")]
public sealed record RefundEscrowCommand(
    Guid OperationId,
    PaymentOperationReference Reference,
    string? Reason = null) : IIntegrationCommand;

[MessageType("concertable.payment.capture-escrow-succeeded.v1")]
public sealed record CaptureEscrowSucceededEvent(
    Guid OperationId,
    PaymentOperationReference Reference) : IIntegrationEvent;

[MessageType("concertable.payment.capture-escrow-rejected.v1")]
public sealed record CaptureEscrowRejectedEvent(
    Guid OperationId,
    PaymentOperationReference Reference,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.deposit-escrow-succeeded.v1")]
public sealed record DepositEscrowSucceededEvent(
    Guid OperationId,
    PaymentOperationReference Reference) : IIntegrationEvent;

[MessageType("concertable.payment.deposit-escrow-rejected.v1")]
public sealed record DepositEscrowRejectedEvent(
    Guid OperationId,
    PaymentOperationReference Reference,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.refund-escrow-succeeded.v1")]
public sealed record RefundEscrowSucceededEvent(
    Guid OperationId,
    PaymentOperationReference Reference) : IIntegrationEvent;

[MessageType("concertable.payment.refund-escrow-rejected.v1")]
public sealed record RefundEscrowRejectedEvent(
    Guid OperationId,
    PaymentOperationReference Reference,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.refund-escrow-deferred.v1")]
public sealed record RefundEscrowDeferredEvent(
    Guid OperationId,
    PaymentOperationReference Reference) : IIntegrationEvent;
