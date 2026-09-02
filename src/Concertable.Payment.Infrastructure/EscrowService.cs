using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class EscrowService : IEscrowService
{
    private readonly IPaymentManager paymentManager;
    private readonly IEscrowRepository escrowRepository;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly ILedgerService ledgerService;
    private readonly IUnitOfWork unitOfWork;
    private readonly ICommissionService commissionService;
    private readonly CommissionCalculator commissionCalculator;
    private readonly Money platformFee;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<EscrowService> logger;

    public EscrowService(
        IPaymentManager paymentManager,
        IEscrowRepository escrowRepository,
        IPayoutAccountRepository payoutAccountRepository,
        ILedgerService ledgerService,
        IUnitOfWork unitOfWork,
        ICommissionService commissionService,
        CommissionCalculator commissionCalculator,
        IOptions<PlatformFeeOptions> platformFeeOptions,
        TimeProvider timeProvider,
        ILogger<EscrowService> logger)
    {
        this.paymentManager = paymentManager;
        this.escrowRepository = escrowRepository;
        this.payoutAccountRepository = payoutAccountRepository;
        this.ledgerService = ledgerService;
        this.unitOfWork = unitOfWork;
        this.commissionService = commissionService;
        this.commissionCalculator = commissionCalculator;
        platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        DepositCoreAsync(payerId, payeeId, amount, paymentMethodId, session, bookingId, null, ct);

    public Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid operationId,
        CancellationToken ct = default) =>
        DepositCoreAsync(payerId, payeeId, amount, paymentMethodId, session, bookingId, operationId, ct);

    public async Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            gross,
            null,
            stripeSetupIntentId,
            ct);
        if (!authorized.TryGetValue(out var bound))
        {
            authorized.TryGetError(out var commissionError);
            return Result<EscrowDeposit, EscrowDepositError>.Failure(
                new EscrowDepositError.CommissionFailure(commissionError!));
        }

        var existing = await escrowRepository.GetByCommissionBindingIdAsync(commissionBindingId, ct);
        if (existing is not null)
            return Result<EscrowDeposit, EscrowDepositError>.Success(
                new EscrowDeposit(existing.Id, existing.ChargeId, existing.Status));

        var payerError = await ValidatePayerAsync(payerId, session, ct);
        if (payerError.TryGetValue(out var error))
            return Result<EscrowDeposit, EscrowDepositError>.Failure(new EscrowDepositError.PaymentFailure(error));

        var calculation = bound.Calculation;
        var hold = await paymentManager.HoldBoundCommissionAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(bound, bookingId, TransactionTypes.Escrow),
            commissionBindingId,
            ct);
        if (!hold.TryGetValue(out var outcome))
        {
            hold.TryGetError(out var paymentError);
            return Result<EscrowDeposit, EscrowDepositError>.Failure(new EscrowDepositError.PaymentFailure(paymentError!));
        }

        commissionService.BindPaymentIntent(bound.Binding, outcome.TransactionId);
        var escrow = EscrowEntity.CreateBound(
            bookingId,
            payerId,
            payeeId,
            commissionBindingId,
            calculation,
            outcome.TransactionId);
        await escrowRepository.AddAsync(escrow, ct);

        if (!outcome.RequiresAction)
        {
            EnsureTransition(escrow.Confirm());
            await ledgerService.StageAsync(
                LedgerPostings.EscrowHold(
                    escrow.FromOwnerId,
                    escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                    escrow.BookingId,
                    escrow.ChargeId),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result<EscrowDeposit, EscrowDepositError>.Success(
            new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, outcome.ClientSecret));
    }

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default) =>
        CaptureCoreAsync(payerId, payeeId, amount, paymentIntentId, bookingId, null, ct);

    public Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        Guid operationId,
        CancellationToken ct = default) =>
        CaptureCoreAsync(payerId, payeeId, amount, paymentIntentId, bookingId, operationId, ct);

    public async Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default)
    {
        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            gross,
            paymentIntentId,
            null,
            ct);
        if (!authorized.TryGetValue(out var bound))
        {
            authorized.TryGetError(out var commissionError);
            return Result<EscrowDeposit, EscrowCaptureError>.Failure(
                new EscrowCaptureError.CommissionFailure(commissionError!));
        }

        var existing = await escrowRepository.GetByCommissionBindingIdAsync(commissionBindingId, ct);
        if (existing is not null)
            return Result<EscrowDeposit, EscrowCaptureError>.Success(
                new EscrowDeposit(existing.Id, existing.ChargeId, existing.Status));

        var capture = await paymentManager.CaptureAsync(new CaptureRequest
        {
            PaymentIntentId = paymentIntentId,
            CommissionBindingId = commissionBindingId,
            Metadata = CommissionMetadata(bound, bookingId, TransactionTypes.Escrow)
        }, ct);
        if (capture.TryGetError(out var paymentError))
            return Result<EscrowDeposit, EscrowCaptureError>.Failure(new EscrowCaptureError.PaymentFailure(paymentError));

        commissionService.BindPaymentIntent(bound.Binding, paymentIntentId);
        var escrow = EscrowEntity.CreateBound(
            bookingId,
            payerId,
            payeeId,
            commissionBindingId,
            bound.Calculation,
            paymentIntentId);
        EnsureTransition(escrow.Confirm());
        await escrowRepository.AddAsync(escrow, ct);
        await ledgerService.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<EscrowDeposit, EscrowCaptureError>.Success(
            new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status));
    }

    public async Task<Result<Transfer, EscrowReleaseError>> ReleaseAsync(
        int escrowId,
        CancellationToken ct = default)
    {
        var result = await ReleaseByIdCoreAsync(escrowId, null, ct);
        if (result.TryGetValue(out var transfer))
            return Result<Transfer, EscrowReleaseError>.Success(transfer);

        result.TryGetError(out var error);
        return error is EscrowReleaseOperationError.ReleaseFailure(var releaseError)
            ? Result<Transfer, EscrowReleaseError>.Failure(releaseError)
            : throw new InvalidOperationException("A legacy escrow release cannot produce an operation conflict.");
    }

    public async Task<Result<Option<Transfer>, EscrowReleaseError>> ReleaseByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        var result = await ReleaseByBookingIdCoreAsync(null, bookingId, ct);
        if (result.TryGetValue(out var transfer))
            return Result<Option<Transfer>, EscrowReleaseError>.Success(transfer);

        result.TryGetError(out var error);
        return error is EscrowReleaseOperationError.ReleaseFailure(var releaseError)
            ? Result<Option<Transfer>, EscrowReleaseError>.Failure(releaseError)
            : throw new InvalidOperationException("A legacy escrow release cannot produce an operation conflict.");
    }

    public Task<Result<Option<Transfer>, EscrowReleaseOperationError>> ReleaseByBookingIdAsync(
        Guid operationId,
        int bookingId,
        CancellationToken ct = default) =>
        ReleaseByBookingIdCoreAsync(operationId, bookingId, ct);

    public Task<Result<Refund, EscrowRefundError>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default) =>
        RefundByIdCoreAsync(escrowId, amount, reason, null, ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default) =>
        RefundByBookingIdCoreAsync(bookingId, amount, reason, null, ct);

    public Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount,
        string? reason,
        Guid operationId,
        CancellationToken ct = default) =>
        RefundByBookingIdCoreAsync(bookingId, amount, reason, operationId, ct);

    public async Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        Money gross,
        string? reason = null,
        CancellationToken ct = default)
    {
        var grossMinor = gross.ToMinorUnits();
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowToRefundForBooking(bookingId);
            return Result<Option<Refund>, EscrowRefundError>.Success(Option.None<Refund>());
        }
        if (escrow.CommissionBindingId is null)
            return Result<Option<Refund>, EscrowRefundError>.Failure(new EscrowRefundError.CommissionBindingNotFound());
        if (gross.Currency != escrow.Currency)
            return Result<Option<Refund>, EscrowRefundError>.Failure(new EscrowRefundError.CurrencyMismatch());
        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result<Option<Refund>, EscrowRefundError>.Failure(new EscrowRefundError.EscrowNotRefundable());
        if (grossMinor <= 0)
            return Result<Option<Refund>, EscrowRefundError>.Failure(new EscrowRefundError.AmountMustBePositive());

        var grossAlreadyRefunded = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.GrossRefundedMinor);
        var cumulativeGrossRefund = checked(grossAlreadyRefunded + grossMinor);
        if (cumulativeGrossRefund > escrow.PayeeGrossMinor)
            return Result<Option<Refund>, EscrowRefundError>.Failure(new EscrowRefundError.AmountExceedsRemaining());

        var cumulativeCommissionRefund = commissionCalculator.CalculateCumulativeRefund(
            escrow.CommissionGrossMinor,
            cumulativeGrossRefund,
            escrow.PayeeGrossMinor);
        var cumulativeVatReversal = commissionCalculator.CalculateCumulativeRefund(
            escrow.CommissionVatMinor,
            cumulativeGrossRefund,
            escrow.PayeeGrossMinor);
        var commissionRefundMinor = checked(
            cumulativeCommissionRefund -
            escrow.Refunds.Where(refund => refund.CountsTowardCumulative).Sum(refund => refund.CommissionRefundedMinor));
        var commissionVatReversedMinor = checked(
            cumulativeVatReversal -
            escrow.Refunds.Where(refund => refund.CountsTowardCumulative).Sum(refund => refund.CommissionVatReversedMinor));

        var refund = await ExecuteRefundAsync(
            escrow,
            grossMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            reason,
            null,
            ct);
        if (!refund.TryGetValue(out var completedRefund))
        {
            refund.TryGetError(out var error);
            return Result<Option<Refund>, EscrowRefundError>.Failure(error!);
        }
        return Result<Option<Refund>, EscrowRefundError>.Success(Option.Some(completedRefund));
    }

    public async Task<Option<EscrowDto>> GetByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        return escrow is null
            ? Option.None<EscrowDto>()
            : Option.Some(new EscrowDto(
                escrow.Id,
                escrow.BookingId,
                escrow.FromOwnerId,
                escrow.ToOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency).Amount,
                escrow.Status,
                escrow.ChargeId,
                escrow.TransferId,
                escrow.ReleasedAt));
    }

    private async Task<Result<EscrowDeposit, EscrowDepositError>> DepositCoreAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid? operationId,
        CancellationToken ct)
    {
        var existing = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (existing is not null)
            return ExistingDeposit(existing, payerId, payeeId, amount);

        var payerError = await ValidatePayerAsync(payerId, session, ct);
        if (payerError.TryGetValue(out var error))
            return new EscrowDepositError.PaymentFailure(error);

        var hold = await HoldAsync(
            payerId,
            payeeId,
            amount + platformFee,
            paymentMethodId,
            session,
            OperationMetadata(operationId, new Dictionary<string, string>
            {
                [PaymentMetadataKeys.Type] = TransactionTypes.Escrow,
                [PaymentMetadataKeys.BookingId] = bookingId.ToString()
            }),
            operationId,
            ct);
        if (!hold.TryGetValue(out var outcome))
        {
            hold.TryGetError(out var paymentError);
            return new EscrowDepositError.PaymentFailure(paymentError!);
        }

        var escrow = EscrowEntity.Create(bookingId, payerId, payeeId, amount, platformFee, outcome.TransactionId);
        await escrowRepository.AddAsync(escrow);
        await unitOfWork.SaveChangesAsync(ct);

        if (!outcome.RequiresAction)
        {
            EnsureTransition(escrow.Confirm());
            await ledgerService.StageAsync(
                LedgerPostings.EscrowHold(
                    escrow.FromOwnerId,
                    escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                    escrow.BookingId,
                    escrow.ChargeId),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, outcome.ClientSecret);
    }

    private async Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureCoreAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        Guid? operationId,
        CancellationToken ct)
    {
        var existing = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (existing is not null)
            return ExistingCapture(existing, payerId, payeeId, amount, paymentIntentId);

        var capture = await paymentManager.CaptureAsync(new CaptureRequest
        {
            PaymentIntentId = paymentIntentId,
            OperationId = operationId,
            Metadata = OperationMetadata(operationId, new Dictionary<string, string>
            {
                [PaymentMetadataKeys.Type] = TransactionTypes.Escrow,
                [PaymentMetadataKeys.BookingId] = bookingId.ToString()
            })
        }, ct);
        if (capture.TryGetError(out var error))
            return new EscrowCaptureError.PaymentFailure(error);

        var escrow = EscrowEntity.Create(bookingId, payerId, payeeId, amount, platformFee, paymentIntentId);
        EnsureTransition(escrow.Confirm());
        await escrowRepository.AddAsync(escrow);
        await ledgerService.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, null);
    }

    private async Task<Result<Transfer, EscrowReleaseOperationError>> ReleaseByIdCoreAsync(
        int escrowId,
        Guid? operationId,
        CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByIdAsync(escrowId, ct);
        if (escrow is null)
            return new EscrowReleaseOperationError.ReleaseFailure(new EscrowReleaseError.EscrowNotFound());

        if (operationId is { } id)
        {
            var fingerprint = SettlementOperationFingerprint.CreateRelease(id, escrow);
            var reserved = await escrowRepository.ReserveReleaseAsync(escrow.Id, id, fingerprint, ct);
            if (reserved.Conflict)
                return new EscrowReleaseOperationError.OperationConflict();

            escrow = reserved.Escrow;
            if (escrow is null)
                return new EscrowReleaseOperationError.ReleaseFailure(new EscrowReleaseError.EscrowNotFound());

            var reservation = escrow.BeginRelease(id, fingerprint);
            if (reservation.TryGetError(out var error))
            {
                return error is EscrowTransitionError.OperationConflict
                    ? new EscrowReleaseOperationError.OperationConflict()
                    : new EscrowReleaseOperationError.ReleaseFailure(new EscrowReleaseError.EscrowNotHeld());
            }
            if (escrow.TransferId is { } existingTransferId)
                return new Transfer(existingTransferId);

        }
        else if (escrow.Status != EscrowStatus.Held)
        {
            return new EscrowReleaseOperationError.ReleaseFailure(new EscrowReleaseError.EscrowNotHeld());
        }

        var release = await paymentManager.ReleaseAsync(new ReleaseRequest
        {
            PayeeId = escrow.ToOwnerId,
            Amount = escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
            ChargeId = escrow.ChargeId,
            OperationId = operationId,
            CommissionBindingId = escrow.CommissionBindingId,
            Metadata = EscrowMetadata(escrow, TransactionTypes.EscrowRelease)
        }, ct);
        if (!release.TryGetValue(out var transfer))
        {
            release.TryGetError(out var paymentError);
            return new EscrowReleaseOperationError.ReleaseFailure(
                new EscrowReleaseError.PaymentFailure(paymentError!));
        }

        EnsureTransition(escrow.Release(transfer.TransferId, timeProvider.GetUtcNow().DateTime));
        await ledgerService.StageAsync(
            LedgerPostings.EscrowRelease(
                escrow.ToOwnerId,
                escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
                escrow.CommissionNetMinor.ToMoney(escrow.Currency),
                escrow.CommissionVatMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                transfer.TransferId),
            ct);
        if (operationId is null)
        {
            await unitOfWork.SaveChangesAsync(ct);
            return transfer;
        }

        if (await unitOfWork.TrySaveChangesAsync(static exception => exception.IsDuplicateKey(), ct))
            return transfer;

        var canonical = await escrowRepository.GetByIdAsync(escrow.Id, ct);
        return canonical?.ReleaseOperationId == operationId && canonical.TransferId is not null
            ? new Transfer(canonical.TransferId)
            : new EscrowReleaseOperationError.OperationConflict();
    }

    private async Task<Result<Option<Transfer>, EscrowReleaseOperationError>> ReleaseByBookingIdCoreAsync(
        Guid? operationId,
        int bookingId,
        CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowFoundForBooking(bookingId);
            return Result<Option<Transfer>, EscrowReleaseOperationError>.Success(Option.None<Transfer>());
        }
        if (operationId is null && escrow.Status != EscrowStatus.Held)
        {
            logger.EscrowNotHeldSkippingRelease(escrow.Id, bookingId, escrow.Status);
            return Result<Option<Transfer>, EscrowReleaseOperationError>.Success(Option.None<Transfer>());
        }

        var release = await ReleaseByIdCoreAsync(escrow.Id, operationId, ct);
        if (!release.TryGetValue(out var transfer))
        {
            release.TryGetError(out var error);
            return error!;
        }
        return Option.Some(transfer);
    }

    private async Task<Result<Refund, EscrowRefundError>> RefundByIdCoreAsync(
        int escrowId,
        Money? amount,
        string? reason,
        Guid? operationId,
        CancellationToken ct)
    {
        var escrow = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct);
        if (escrow is null)
            return new EscrowRefundError.EscrowNotFound();
        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return new EscrowRefundError.EscrowNotRefundable();

        var refundedTotalMinor = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.PayerTotalRefundedMinor);
        var remainingTotalMinor = checked(escrow.PayerTotalMinor - refundedTotalMinor);
        var refundTotal = amount?.ToMinorUnits() ?? remainingTotalMinor;
        if (amount is not null && amount.Value.Currency != escrow.Currency)
            return new EscrowRefundError.CurrencyMismatch();
        if (refundTotal <= 0)
            return new EscrowRefundError.AmountMustBePositive();
        if (refundTotal > remainingTotalMinor)
            return new EscrowRefundError.AmountExceedsRemaining();

        var refundedGrossMinor = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.GrossRefundedMinor);
        var remainingGrossMinor = checked(escrow.PayeeGrossMinor - refundedGrossMinor);
        var grossRefundMinor = Math.Min(refundTotal, remainingGrossMinor);
        var commissionRefundMinor = checked(refundTotal - grossRefundMinor);
        return await ExecuteRefundAsync(escrow, grossRefundMinor, commissionRefundMinor, 0, reason, operationId, ct);
    }

    private async Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdCoreAsync(
        int bookingId,
        Money? amount,
        string? reason,
        Guid? operationId,
        CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowToRefundForBooking(bookingId);
            Option<Refund> none = null;
            return none;
        }

        var operationRefund = operationId is null
            ? null
            : escrow.Refunds.SingleOrDefault(refund => refund.OperationId == operationId);
        if (operationRefund?.Status == PaymentRefundStatus.Completed)
        {
            Option<Refund> replayed = new Refund(operationRefund.StripeRefundId
                ?? throw new InvalidOperationException("Completed refund has no Stripe reference."));
            return replayed;
        }
        if (operationRefund?.Status == PaymentRefundStatus.Pending)
        {
            var resumed = await ExecuteReservedRefundAsync(escrow, operationRefund, reason, ct);
            if (resumed.TryGetError(out var error))
                return error;
            resumed.TryGetValue(out var resumedRefund);
            Option<Refund> resumedOption = resumedRefund!;
            return resumedOption;
        }

        if (escrow.Status == EscrowStatus.Refunded)
        {
            logger.EscrowAlreadyRefunded(escrow.Id, bookingId);
            Option<Refund> none = null;
            return none;
        }

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
        {
            logger.EscrowNotRefundableSkippingRefund(escrow.Id, bookingId, escrow.Status);
            Option<Refund> none = null;
            return none;
        }

        var refund = await RefundByIdCoreAsync(escrow.Id, amount, reason, operationId, ct);
        if (!refund.TryGetValue(out var completedRefund))
        {
            refund.TryGetError(out var error);
            return error!;
        }
        Option<Refund> completed = completedRefund;
        return completed;
    }

    private async Task<Result<Refund, EscrowRefundError>> ExecuteRefundAsync(
        EscrowEntity escrow,
        long grossRefundMinor,
        long commissionRefundMinor,
        long commissionVatReversedMinor,
        string? reason,
        Guid? operationId,
        CancellationToken ct)
    {
        if (!await escrowRepository.TryReserveRefundGrossAsync(escrow.Id, grossRefundMinor, ct))
            return await ReservationConflictAsync(escrow.Id, grossRefundMinor, ct);

        var reservation = PaymentRefundEntity.CreatePendingForEscrow(
            escrow.Id,
            grossRefundMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            timeProvider.GetUtcNow(),
            operationId);
        if (escrow.RecordRefund(reservation).IsFailure)
        {
            await escrowRepository.ReleaseReservedRefundGrossAsync(escrow.Id, grossRefundMinor, ct);
            throw new InvalidOperationException("Escrow refund reservation could not be recorded.");
        }

        await unitOfWork.SaveChangesAsync(ct);

        return await ExecuteReservedRefundAsync(escrow, reservation, reason, ct);
    }

    private async Task<Result<Refund, EscrowRefundError>> ExecuteReservedRefundAsync(
        EscrowEntity escrow,
        PaymentRefundEntity reservation,
        string? reason,
        CancellationToken ct)
    {
        var grossRefundMinor = reservation.GrossRefundedMinor;
        var commissionRefundMinor = reservation.CommissionRefundedMinor;
        var commissionVatReversedMinor = reservation.CommissionVatReversedMinor;
        var payerTotalRefundMinor = reservation.PayerTotalRefundedMinor;
        var cumulativeGrossRefundMinor = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.GrossRefundedMinor);
        var metadata = OperationMetadata(
            reservation.OperationId,
            EscrowMetadata(escrow, TransactionTypes.EscrowRefund));
        metadata[PaymentMetadataKeys.PayeeGrossMinor] = grossRefundMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionGrossMinor] = commissionRefundMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionVatMinor] = commissionVatReversedMinor.ToString();
        metadata[PaymentMetadataKeys.PayerTotalMinor] = payerTotalRefundMinor.ToString();
        metadata[PaymentMetadataKeys.CumulativeGrossRefundMinor] = cumulativeGrossRefundMinor.ToString();

        var refund = await paymentManager.RefundAsync(new RefundRequest
        {
            Amount = payerTotalRefundMinor.ToMoney(escrow.Currency),
            PaymentIntentId = escrow.ChargeId,
            TransferReversal = escrow.TransferId is null
                ? null
                : new TransferReversal(escrow.TransferId, grossRefundMinor.ToMoney(escrow.Currency)),
            Reason = reason,
            OperationId = reservation.OperationId,
            CommissionBindingId = escrow.CommissionBindingId,
            CumulativeGrossRefundMinor = cumulativeGrossRefundMinor,
            Metadata = metadata
        }, ct);
        if (!refund.TryGetValue(out var completedRefund))
        {
            if (escrow.ReleaseRefund(reservation).IsFailure)
                throw new InvalidOperationException("Escrow refund reservation could not be released.");
            await unitOfWork.SaveChangesAsync(ct);
            await escrowRepository.ReleaseReservedRefundGrossAsync(escrow.Id, grossRefundMinor, ct);
            refund.TryGetError(out var error);
            return new EscrowRefundError.PaymentFailure(error!);
        }

        if (escrow.CompleteRefund(reservation, completedRefund.RefundId, timeProvider.GetUtcNow()).IsFailure)
            throw new InvalidOperationException("Escrow refund reservation could not be completed.");

        var refundPosting = escrow.TransferId is null
            ? LedgerPostings.EscrowRefundBeforeRelease(
                escrow.FromOwnerId,
                payerTotalRefundMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                completedRefund.RefundId)
            : LedgerPostings.EscrowRefundAfterRelease(
                escrow.FromOwnerId,
                escrow.ToOwnerId,
                grossRefundMinor.ToMoney(escrow.Currency),
                checked(commissionRefundMinor - commissionVatReversedMinor).ToMoney(escrow.Currency),
                commissionVatReversedMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                completedRefund.RefundId);
        await ledgerService.StageAsync(refundPosting, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return completedRefund;
    }

    private async Task<Result<Refund, EscrowRefundError>> ReservationConflictAsync(
        int escrowId,
        long grossRefundMinor,
        CancellationToken ct)
    {
        var current = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct);
        if (current is null)
            return new EscrowRefundError.EscrowNotFound();
        if (current.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return new EscrowRefundError.EscrowNotRefundable();
        return checked(current.RefundedGrossMinor + grossRefundMinor) > current.PayeeGrossMinor
            ? new EscrowRefundError.AmountExceedsRemaining()
            : new EscrowRefundError.Conflict();
    }

    private async Task<Option<PaymentError>> ValidatePayerAsync(
        Guid payerId,
        PaymentSession session,
        CancellationToken ct)
    {
        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return new PaymentError.PayerNotFound();
        return session == PaymentSession.OffSession && payer.StripeCustomerId is null
            ? new PaymentError.PayerUnavailable()
            : null;
    }

    private Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        CancellationToken ct) =>
        operationId is { } id
            ? paymentManager.HoldAsync(payerId, payeeId, amount, paymentMethodId, session, metadata, id, ct)
            : paymentManager.HoldAsync(payerId, payeeId, amount, paymentMethodId, session, metadata, ct);

    private static Result<EscrowDeposit, EscrowDepositError> ExistingDeposit(
        EscrowEntity escrow,
        Guid payerId,
        Guid payeeId,
        Money amount)
    {
        EnsureEscrowMatches(escrow, payerId, payeeId, amount, escrow.ChargeId);
        return new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status);
    }

    private static Result<EscrowDeposit, EscrowCaptureError> ExistingCapture(
        EscrowEntity escrow,
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId)
    {
        EnsureEscrowMatches(escrow, payerId, payeeId, amount, paymentIntentId);
        return new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status);
    }

    private static void EnsureEscrowMatches(
        EscrowEntity escrow,
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId)
    {
        if (escrow.FromOwnerId != payerId ||
            escrow.ToOwnerId != payeeId ||
            escrow.Currency != amount.Currency ||
            escrow.PayeeGrossMinor != amount.ToMinorUnits() ||
            escrow.ChargeId != paymentIntentId)
            throw new InvalidOperationException($"Booking {escrow.BookingId} was reused for a different escrow request.");
    }

    private static Dictionary<string, string> OperationMetadata(
        Guid? operationId,
        IReadOnlyDictionary<string, string> metadata)
    {
        var result = metadata.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (operationId is not null)
            result[PaymentMetadataKeys.OperationId] = operationId.Value.ToString();
        return result;
    }

    private static void EnsureTransition<TError>(UnitResult<TError> transition)
        where TError : notnull
    {
        if (transition.IsFailure)
            throw new InvalidOperationException("A newly-created payment entity rejected its initial transition.");
    }

    private static IReadOnlyDictionary<string, string> CommissionMetadata(
        BoundCommission authorized,
        int bookingId,
        string transactionType)
    {
        var calculation = authorized.Calculation;
        return new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Type] = transactionType,
            [PaymentMetadataKeys.BookingId] = bookingId.ToString(),
            [PaymentMetadataKeys.CommissionBindingId] = authorized.Binding.Id.ToString(),
            [PaymentMetadataKeys.Currency] = calculation.Currency.ToString().ToLowerInvariant(),
            [PaymentMetadataKeys.PayeeGrossMinor] = calculation.PayeeGrossMinor.ToString(),
            [PaymentMetadataKeys.CommissionGrossMinor] = calculation.CommissionGrossMinor.ToString(),
            [PaymentMetadataKeys.CommissionNetMinor] = calculation.CommissionNetMinor.ToString(),
            [PaymentMetadataKeys.CommissionVatMinor] = calculation.CommissionVatMinor.ToString(),
            [PaymentMetadataKeys.PayerTotalMinor] = calculation.PayerTotalMinor.ToString()
        };
    }

    private static Dictionary<string, string> EscrowMetadata(
        EscrowEntity escrow,
        string transactionType)
    {
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Type] = transactionType,
            [PaymentMetadataKeys.EscrowId] = escrow.Id.ToString(),
            [PaymentMetadataKeys.BookingId] = escrow.BookingId.ToString()
        };
        if (escrow.CommissionBindingId is not null)
            metadata[PaymentMetadataKeys.CommissionBindingId] = escrow.CommissionBindingId.Value.ToString();
        return metadata;
    }
}
