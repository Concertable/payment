using Concertable.Kernel.ValueObjects;
using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts;

public enum FinancialOperationType
{
    CaptureEscrow,
    DepositEscrow,
    RefundEscrow
}

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

[MessageType("concertable.payment.financial-operation-succeeded.v1")]
public sealed record FinancialOperationSucceededEvent(
    Guid OperationId,
    int BookingId,
    FinancialOperationType Type,
    string ReferenceId) : IIntegrationEvent;

[MessageType("concertable.payment.financial-operation-rejected.v1")]
public sealed record FinancialOperationRejectedEvent(
    Guid OperationId,
    int BookingId,
    FinancialOperationType Type,
    string Code,
    string Message) : IIntegrationEvent;

[MessageType("concertable.payment.financial-operation-deferred.v1")]
public sealed record FinancialOperationDeferredEvent(
    Guid OperationId,
    int BookingId,
    FinancialOperationType Type) : IIntegrationEvent;
