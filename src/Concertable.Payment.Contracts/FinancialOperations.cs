using Concertable.Kernel.ValueObjects;
using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts;

[MessageType("concertable.payment.capture-escrow.v1")]
public sealed record CaptureEscrowCommand(
    Guid OperationId,
    int BookingId,
    Guid PayerId,
    Guid PayeeId,
    long AmountMinor,
    Currency Currency,
    string PaymentIntentId) : IIntegrationCommand;

[MessageType("concertable.payment.deposit-escrow.v1")]
public sealed record DepositEscrowCommand(
    Guid OperationId,
    int BookingId,
    Guid PayerId,
    Guid PayeeId,
    long AmountMinor,
    Currency Currency,
    string PaymentMethodId,
    PaymentSession Session) : IIntegrationCommand;

[MessageType("concertable.payment.refund-escrow.v1")]
public sealed record RefundEscrowCommand(
    Guid OperationId,
    int BookingId,
    string? Reason = null) : IIntegrationCommand;

[MessageType("concertable.payment.capture-escrow-succeeded.v1")]
public sealed record CaptureEscrowSucceededEvent(
    Guid OperationId,
    int BookingId,
    string ReferenceId) : IIntegrationEvent;

[MessageType("concertable.payment.capture-escrow-rejected.v1")]
public sealed record CaptureEscrowRejectedEvent(
    Guid OperationId,
    int BookingId,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.deposit-escrow-succeeded.v1")]
public sealed record DepositEscrowSucceededEvent(
    Guid OperationId,
    int BookingId,
    string ReferenceId) : IIntegrationEvent;

[MessageType("concertable.payment.deposit-escrow-rejected.v1")]
public sealed record DepositEscrowRejectedEvent(
    Guid OperationId,
    int BookingId,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.refund-escrow-succeeded.v1")]
public sealed record RefundEscrowSucceededEvent(
    Guid OperationId,
    int BookingId,
    string ReferenceId) : IIntegrationEvent;

[MessageType("concertable.payment.refund-escrow-rejected.v1")]
public sealed record RefundEscrowRejectedEvent(
    Guid OperationId,
    int BookingId,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.refund-escrow-deferred.v1")]
public sealed record RefundEscrowDeferredEvent(
    Guid OperationId,
    int BookingId) : IIntegrationEvent;
