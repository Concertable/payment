using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Concertable.Messaging.Contracts;
using Reunion.Errors;

namespace Concertable.Payment.Infrastructure.Handlers;

internal sealed class FinancialOperationHandler :
    IIntegrationCommandHandler<CaptureEscrowCommand>,
    IIntegrationCommandHandler<CaptureEscrowByReferenceCommand>,
    IIntegrationCommandHandler<DepositEscrowCommand>,
    IIntegrationCommandHandler<DepositEscrowByReferenceCommand>,
    IIntegrationCommandHandler<RefundEscrowCommand>
{
    private readonly IEscrowService escrowService;
    private readonly IFinancialOperationRepository operationRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IBus bus;
    private readonly IOutboxUnitOfWorkBehavior outboxBehavior;
    private readonly IPaymentOperationResolver paymentOperationResolver;
    private readonly TimeProvider timeProvider;

    public FinancialOperationHandler(
        IEscrowService escrowService,
        IFinancialOperationRepository operationRepository,
        IUnitOfWork unitOfWork,
        IBus bus,
        IOutboxUnitOfWorkBehavior outboxBehavior,
        IPaymentOperationResolver paymentOperationResolver,
        TimeProvider timeProvider)
    {
        this.escrowService = escrowService;
        this.operationRepository = operationRepository;
        this.unitOfWork = unitOfWork;
        this.bus = bus;
        this.outboxBehavior = outboxBehavior;
        this.paymentOperationResolver = paymentOperationResolver;
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
            Fingerprint(
                nameof(CaptureEscrowCommand),
                command.BookingId,
                command.PayerId,
                command.PayeeId,
                command.AmountMinor,
                command.Currency,
                command.PaymentIntentId),
            ct);
        if (await ReplayTerminalAsync(
                operation,
                reference => new CaptureEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
                (code, message) => new CaptureEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct))
            return;

        var result = await escrowService.CaptureAsync(
            command.PayerId,
            command.PayeeId,
            Money.FromMinorUnits(command.AmountMinor, command.Currency),
            command.PaymentIntentId,
            command.BookingId,
            command.OperationId,
            ct);

        await CompleteAsync(
            operation,
            result,
            deposit => deposit.ChargeId,
            reference => new CaptureEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
            (code, message) => new CaptureEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
            ct);
    }

    public async Task HandleAsync(
        DepositEscrowCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            Fingerprint(
                nameof(DepositEscrowCommand),
                command.BookingId,
                command.PayerId,
                command.PayeeId,
                command.AmountMinor,
                command.Currency,
                command.PaymentMethodId,
                command.Session),
            ct);
        if (await ReplayTerminalAsync(
                operation,
                reference => new DepositEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
                (code, message) => new DepositEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct))
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

        await CompleteAsync(
            operation,
            result,
            deposit => deposit.ChargeId,
            reference => new DepositEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
            (code, message) => new DepositEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
            ct);
    }

    public async Task HandleAsync(
        DepositEscrowByReferenceCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            Fingerprint(
                nameof(DepositEscrowByReferenceCommand),
                command.BookingId,
                command.PayerId,
                command.PayeeId,
                command.AmountMinor,
                command.Currency,
                command.PaymentMethod.OperationType,
                command.PaymentMethod.ConsumerCorrelation,
                command.Session),
            ct);
        if (await ReplayTerminalAsync(
                operation,
                reference => new DepositEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
                (code, message) => new DepositEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct))
            return;

        var paymentMethod = await paymentOperationResolver.ResolvePaymentMethodAsync(
            command.PaymentMethod,
            command.PayerId,
            ct);
        if (!paymentMethod.TryGetValue(out var paymentMethodId))
        {
            paymentMethod.TryGetError(out var error);
            await RejectAsync(
                operation,
                error!,
                (code, message) => new DepositEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct);
            return;
        }

        var result = await escrowService.DepositAsync(
            command.PayerId,
            command.PayeeId,
            Money.FromMinorUnits(command.AmountMinor, command.Currency),
            paymentMethodId,
            command.Session,
            command.BookingId,
            command.OperationId,
            ct);

        await CompleteAsync(
            operation,
            result,
            deposit => deposit.ChargeId,
            reference => new DepositEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
            (code, message) => new DepositEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
            ct);
    }

    public async Task HandleAsync(
        CaptureEscrowByReferenceCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            Fingerprint(
                nameof(CaptureEscrowByReferenceCommand),
                command.BookingId,
                command.PayerId,
                command.PayeeId,
                command.AmountMinor,
                command.Currency,
                command.Authorization.OperationType,
                command.Authorization.ConsumerCorrelation),
            ct);
        if (await ReplayTerminalAsync(
                operation,
                reference => new CaptureEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
                (code, message) => new CaptureEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct))
        {
            return;
        }

        var authorization = await paymentOperationResolver.ResolveAuthorizationAsync(
            command.Authorization,
            command.PayerId,
            ct);
        if (!authorization.TryGetValue(out var paymentIntentId))
        {
            authorization.TryGetError(out var error);
            await RejectAsync(
                operation,
                error!,
                (code, message) => new CaptureEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct);
            return;
        }

        var result = await escrowService.CaptureAsync(
            command.PayerId,
            command.PayeeId,
            Money.FromMinorUnits(command.AmountMinor, command.Currency),
            paymentIntentId,
            command.BookingId,
            command.OperationId,
            ct);

        await CompleteAsync(
            operation,
            result,
            deposit => deposit.ChargeId,
            reference => new CaptureEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
            (code, message) => new CaptureEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
            ct);
    }

    public async Task HandleAsync(
        RefundEscrowCommand command,
        MessageEnvelope envelope,
        CancellationToken ct = default)
    {
        var operation = await PrepareAsync(
            command.OperationId,
            command.BookingId,
            Fingerprint(nameof(RefundEscrowCommand), command.BookingId, command.Reason),
            ct);
        if (await ReplayTerminalAsync(
                operation,
                reference => new RefundEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
                (code, message) => new RefundEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct))
            return;

        var result = await escrowService.RefundByBookingIdAsync(
            command.BookingId,
            amount: null,
            reason: command.Reason,
            operationId: command.OperationId,
            ct: ct);

        if (result.TryGetError(out var error))
        {
            await RejectAsync(
                operation,
                error,
                (code, message) => new RefundEscrowRejectedEvent(operation.Id, operation.BookingId, code, message),
                ct);
            return;
        }

        result.TryGetValue(out var refund);
        if (refund.TryGetValue(out var value))
        {
            await SucceedAsync(
                operation,
                value.RefundId,
                reference => new RefundEscrowSucceededEvent(operation.Id, operation.BookingId, reference),
                ct);
            return;
        }

        operation.RecordAttempt(timeProvider.GetUtcNow());
        await outboxBehavior.ExecuteAsync(
            () => bus.PublishAsync(new RefundEscrowDeferredEvent(operation.Id, operation.BookingId), ct),
            ct);
    }

    private async Task<FinancialOperationEntity> PrepareAsync(
        Guid id,
        int bookingId,
        string fingerprint,
        CancellationToken ct)
    {
        var operation = await operationRepository.GetAsync(id, ct);
        if (operation is not null)
        {
            operation.EnsureMatches(bookingId, fingerprint);
            return operation;
        }

        operation = FinancialOperationEntity.Create(id, bookingId, fingerprint, timeProvider.GetUtcNow());
        await operationRepository.AddAsync(operation, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return operation;
    }

    private async Task<bool> ReplayTerminalAsync<TSucceeded, TRejected>(
        FinancialOperationEntity operation,
        Func<string, TSucceeded> success,
        Func<string, string, TRejected> rejection,
        CancellationToken ct)
        where TSucceeded : IIntegrationEvent
        where TRejected : IIntegrationEvent
    {
        switch (operation.Status)
        {
            case FinancialOperationStatus.Pending:
                return false;
            case FinancialOperationStatus.Succeeded:
                await outboxBehavior.ExecuteAsync(
                    () => bus.PublishAsync(success(operation.ReferenceId
                        ?? throw new InvalidOperationException("Succeeded operation has no reference.")), ct),
                    ct);
                return true;
            case FinancialOperationStatus.Rejected:
                await outboxBehavior.ExecuteAsync(
                    () => bus.PublishAsync(rejection(
                        operation.FailureCode
                            ?? throw new InvalidOperationException("Rejected operation has no code."),
                        operation.FailureMessage
                            ?? throw new InvalidOperationException("Rejected operation has no message.")), ct),
                    ct);
                return true;
            default:
                throw new InvalidOperationException($"Unknown financial operation status {operation.Status}.");
        }
    }

    private async Task CompleteAsync<TValue, TError, TSucceeded, TRejected>(
        FinancialOperationEntity operation,
        Result<TValue, TError> result,
        Func<TValue, string> reference,
        Func<string, TSucceeded> success,
        Func<string, string, TRejected> rejection,
        CancellationToken ct)
        where TValue : notnull
        where TError : IError
        where TSucceeded : IIntegrationEvent
        where TRejected : IIntegrationEvent
    {
        if (result.TryGetError(out var error))
        {
            await RejectAsync(operation, error, rejection, ct);
            return;
        }

        result.TryGetValue(out var value);
        await SucceedAsync(operation, reference(value!), success, ct);
    }

    private Task SucceedAsync<TEvent>(
        FinancialOperationEntity operation,
        string referenceId,
        Func<string, TEvent> outcome,
        CancellationToken ct)
        where TEvent : IIntegrationEvent =>
        outboxBehavior.ExecuteAsync(async () =>
        {
            operation.Succeed(referenceId, timeProvider.GetUtcNow());
            await bus.PublishAsync(outcome(referenceId), ct);
        }, ct);

    private Task RejectAsync<TError, TEvent>(
        FinancialOperationEntity operation,
        TError error,
        Func<string, string, TEvent> outcome,
        CancellationToken ct)
        where TError : IError
        where TEvent : IIntegrationEvent =>
        outboxBehavior.ExecuteAsync(async () =>
        {
            operation.Reject(error.Definition.Code, error.Definition.Message, timeProvider.GetUtcNow());
            await bus.PublishAsync(outcome(error.Definition.Code, error.Definition.Message), ct);
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
