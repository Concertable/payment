using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Concertable.Messaging.Contracts;
using Reunion.Errors;

namespace Concertable.Payment.Infrastructure.Handlers;

internal sealed class FinancialOperationHandler :
    IIntegrationCommandHandler<CaptureEscrowCommand>,
    IIntegrationCommandHandler<DepositEscrowCommand>,
    IIntegrationCommandHandler<RefundEscrowCommand>
{
    private readonly IEscrowService escrowService;
    private readonly IFinancialOperationRepository operationRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IBus bus;
    private readonly IOutboxUnitOfWorkBehavior outboxBehavior;
    private readonly TimeProvider timeProvider;

    public FinancialOperationHandler(
        IEscrowService escrowService,
        IFinancialOperationRepository operationRepository,
        IUnitOfWork unitOfWork,
        IBus bus,
        IOutboxUnitOfWorkBehavior outboxBehavior,
        TimeProvider timeProvider)
    {
        this.escrowService = escrowService;
        this.operationRepository = operationRepository;
        this.unitOfWork = unitOfWork;
        this.bus = bus;
        this.outboxBehavior = outboxBehavior;
        this.timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        CaptureEscrowCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            FinancialOperationType.CaptureEscrow,
            Fingerprint(
                command.BookingId,
                command.PayerId,
                command.PayeeId,
                command.AmountMinor,
                command.Currency,
                command.PaymentIntentId),
            ct);
        if (await ReplayTerminalAsync(operation, ct))
            return;

        var result = await escrowService.CaptureAsync(
            command.PayerId,
            command.PayeeId,
            Money.FromMinorUnits(command.AmountMinor, command.Currency),
            command.PaymentIntentId,
            command.BookingId,
            command.OperationId,
            ct);

        await CompleteAsync(operation, result, deposit => deposit.ChargeId, ct);
    }

    public async Task HandleAsync(
        DepositEscrowCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            FinancialOperationType.DepositEscrow,
            Fingerprint(
                command.BookingId,
                command.PayerId,
                command.PayeeId,
                command.AmountMinor,
                command.Currency,
                command.PaymentMethodId,
                command.Session),
            ct);
        if (await ReplayTerminalAsync(operation, ct))
            return;

        var result = await escrowService.DepositAsync(
            command.PayerId,
            command.PayeeId,
            Money.FromMinorUnits(command.AmountMinor, command.Currency),
            command.PaymentMethodId,
            command.Session,
            command.BookingId,
            command.OperationId,
            ct);

        await CompleteAsync(operation, result, deposit => deposit.ChargeId, ct);
    }

    public async Task HandleAsync(
        RefundEscrowCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            FinancialOperationType.RefundEscrow,
            Fingerprint(command.BookingId, command.Reason),
            ct);
        if (await ReplayTerminalAsync(operation, ct))
            return;

        var result = await escrowService.RefundByBookingIdAsync(
            command.BookingId,
            amount: null,
            reason: command.Reason,
            operationId: command.OperationId,
            ct: ct);

        if (result.TryGetError(out var error))
        {
            await RejectAsync(operation, error, ct);
            return;
        }

        result.TryGetValue(out var refund);
        if (refund.TryGetValue(out var value))
        {
            await SucceedAsync(operation, value.RefundId, ct);
            return;
        }

        operation.RecordAttempt(timeProvider.GetUtcNow());
        await outboxBehavior.ExecuteAsync(
            () => bus.PublishAsync(
                new FinancialOperationDeferredEvent(operation.Id, operation.BookingId, operation.Type),
                ct),
            ct);
    }

    private async Task<FinancialOperationEntity> PrepareAsync(
        Guid id,
        int bookingId,
        FinancialOperationType type,
        string fingerprint,
        CancellationToken ct)
    {
        var operation = await operationRepository.GetAsync(id, ct);
        if (operation is not null)
        {
            operation.EnsureMatches(bookingId, type, fingerprint);
            return operation;
        }

        operation = FinancialOperationEntity.Create(id, bookingId, type, fingerprint, timeProvider.GetUtcNow());
        await operationRepository.AddAsync(operation, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return operation;
    }

    private Task<bool> ReplayTerminalAsync(FinancialOperationEntity operation, CancellationToken ct) =>
        operation.Status switch
        {
            FinancialOperationStatus.Pending => Task.FromResult(false),
            FinancialOperationStatus.Succeeded => PublishReplayAsync(
                new FinancialOperationSucceededEvent(
                    operation.Id,
                    operation.BookingId,
                    operation.Type,
                    operation.ReferenceId ?? throw new InvalidOperationException("Succeeded operation has no reference.")),
                ct),
            FinancialOperationStatus.Rejected => PublishReplayAsync(
                new FinancialOperationRejectedEvent(
                    operation.Id,
                    operation.BookingId,
                    operation.Type,
                    operation.FailureCode ?? throw new InvalidOperationException("Rejected operation has no code."),
                    operation.FailureMessage ?? throw new InvalidOperationException("Rejected operation has no message.")),
                ct),
            _ => throw new InvalidOperationException($"Unknown financial operation status {operation.Status}.")
        };

    private async Task<bool> PublishReplayAsync<TEvent>(TEvent @event, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        await outboxBehavior.ExecuteAsync(() => bus.PublishAsync(@event, ct), ct);
        return true;
    }

    private async Task CompleteAsync<TValue, TError>(
        FinancialOperationEntity operation,
        Result<TValue, TError> result,
        Func<TValue, string> reference,
        CancellationToken ct)
        where TValue : notnull
        where TError : IError
    {
        if (result.TryGetError(out var error))
        {
            await RejectAsync(operation, error, ct);
            return;
        }

        result.TryGetValue(out var value);
        await SucceedAsync(operation, reference(value!), ct);
    }

    private Task SucceedAsync(
        FinancialOperationEntity operation,
        string referenceId,
        CancellationToken ct) =>
        outboxBehavior.ExecuteAsync(async () =>
        {
            operation.Succeed(referenceId, timeProvider.GetUtcNow());
            await bus.PublishAsync(
                new FinancialOperationSucceededEvent(
                    operation.Id,
                    operation.BookingId,
                    operation.Type,
                    referenceId),
                ct);
        }, ct);

    private Task RejectAsync<TError>(
        FinancialOperationEntity operation,
        TError error,
        CancellationToken ct)
        where TError : IError =>
        outboxBehavior.ExecuteAsync(async () =>
        {
            operation.Reject(error.Definition.Code, error.Definition.Message, timeProvider.GetUtcNow());
            await bus.PublishAsync(
                new FinancialOperationRejectedEvent(
                    operation.Id,
                    operation.BookingId,
                    operation.Type,
                    error.Definition.Code,
                    error.Definition.Message),
                ct);
        }, ct);

    private static string Fingerprint(params object?[] values)
    {
        var serialized = string.Join(
            '\u001f',
            values.Select(value => value switch
            {
                null => string.Empty,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}
