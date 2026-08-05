using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
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
    private readonly ILedgerService ledger;
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
        ILedgerService ledger,
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
        this.ledger = ledger;
        this.unitOfWork = unitOfWork;
        this.commissionService = commissionService;
        this.commissionCalculator = commissionCalculator;
        this.platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var payerError = await ValidatePayerAsync(payerId, session, ct);
        if (payerError.TryGetValue(out var error))
            return Result<EscrowDeposit, EscrowDepositError>.Failure(new EscrowDepositError.PaymentFailure(error));

        var hold = await paymentManager.HoldAsync(
            payerId,
            payeeId,
            amount + platformFee,
            paymentMethodId,
            session,
            new Dictionary<string, string>
            {
                [PaymentMetadataKeys.Type] = TransactionTypes.Escrow,
                [PaymentMetadataKeys.BookingId] = bookingId.ToString()
            },
            ct);
        if (!hold.TryGetValue(out var outcome))
        {
            hold.TryGetError(out var paymentError);
            return Result<EscrowDeposit, EscrowDepositError>.Failure(new EscrowDepositError.PaymentFailure(paymentError!));
        }
        if (string.IsNullOrEmpty(outcome.TransactionId))
            throw new InvalidOperationException("Stripe hold response missing PaymentIntent id.");

        var escrow = EscrowEntity.Create(bookingId, payerId, payeeId, amount, platformFee, outcome.TransactionId);
        await escrowRepository.AddAsync(escrow);
        await unitOfWork.SaveChangesAsync(ct);

        if (!outcome.RequiresAction)
        {
            EnsureTransition(escrow.Confirm());
            await ledger.StageAsync(
                LedgerPostings.EscrowHold(
                    escrow.FromOwnerId,
                    escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                    escrow.BookingId,
                    escrow.ChargeId),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Result<EscrowDeposit, EscrowDepositError>.Success(
            new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, outcome.ClientSecret));
    }

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
        var hold = await paymentManager.HoldAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(bound, bookingId, TransactionTypes.Escrow),
            ct);
        if (!hold.TryGetValue(out var outcome))
        {
            hold.TryGetError(out var paymentError);
            return Result<EscrowDeposit, EscrowDepositError>.Failure(new EscrowDepositError.PaymentFailure(paymentError!));
        }
        if (string.IsNullOrEmpty(outcome.TransactionId))
            throw new InvalidOperationException("Stripe hold response missing PaymentIntent id.");

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
            await ledger.StageAsync(
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

    public async Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default)
    {
        var capture = await paymentManager.CaptureAsync(new CaptureRequest
        {
            PaymentIntentId = paymentIntentId,
            Metadata = new Dictionary<string, string>
            {
                [PaymentMetadataKeys.Type] = TransactionTypes.Escrow,
                [PaymentMetadataKeys.BookingId] = bookingId.ToString()
            }
        }, ct);
        if (capture.TryGetError(out var error))
            return Result<EscrowDeposit, EscrowCaptureError>.Failure(new EscrowCaptureError.PaymentFailure(error));

        var escrow = EscrowEntity.Create(bookingId, payerId, payeeId, amount, platformFee, paymentIntentId);
        EnsureTransition(escrow.Confirm());
        await escrowRepository.AddAsync(escrow);
        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<EscrowDeposit, EscrowCaptureError>.Success(
            new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, null));
    }

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
        await ledger.StageAsync(
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
        var escrow = await escrowRepository.GetByIdAsync(escrowId);
        if (escrow is null)
            return Result<Transfer, EscrowReleaseError>.Failure(new EscrowReleaseError.EscrowNotFound());
        if (escrow.Status != EscrowStatus.Held)
            return Result<Transfer, EscrowReleaseError>.Failure(new EscrowReleaseError.EscrowNotHeld());

        var release = await paymentManager.ReleaseAsync(new ReleaseRequest
        {
            PayeeId = escrow.ToOwnerId,
            Amount = escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
            ChargeId = escrow.ChargeId,
            Metadata = EscrowMetadata(escrow, TransactionTypes.EscrowRelease)
        }, ct);
        if (!release.TryGetValue(out var transfer))
        {
            release.TryGetError(out var paymentError);
            return Result<Transfer, EscrowReleaseError>.Failure(new EscrowReleaseError.PaymentFailure(paymentError!));
        }

        EnsureTransition(escrow.Release(transfer.TransferId, timeProvider.GetUtcNow().DateTime));
        await ledger.StageAsync(
            LedgerPostings.EscrowRelease(
                escrow.ToOwnerId,
                escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
                escrow.CommissionNetMinor.ToMoney(escrow.Currency),
                escrow.CommissionVatMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                transfer.TransferId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<Transfer, EscrowReleaseError>.Success(transfer);
    }

    public async Task<Result<Option<Transfer>, EscrowReleaseError>> ReleaseByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowFoundForBooking(bookingId);
            return Result<Option<Transfer>, EscrowReleaseError>.Success(Option.None<Transfer>());
        }
        if (escrow.Status != EscrowStatus.Held)
        {
            logger.EscrowNotHeldSkippingRelease(escrow.Id, bookingId, escrow.Status);
            return Result<Option<Transfer>, EscrowReleaseError>.Success(Option.None<Transfer>());
        }

        var release = await ReleaseAsync(escrow.Id, ct);
        if (!release.TryGetValue(out var transfer))
        {
            release.TryGetError(out var error);
            return Result<Option<Transfer>, EscrowReleaseError>.Failure(error!);
        }
        return Result<Option<Transfer>, EscrowReleaseError>.Success(Option.Some(transfer));
    }

    public async Task<Result<Refund, EscrowRefundError>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct);
        if (escrow is null)
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.EscrowNotFound());
        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.EscrowNotRefundable());

        var refundedTotalMinor = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.PayerTotalRefundedMinor);
        var remainingTotalMinor = checked(escrow.PayerTotalMinor - refundedTotalMinor);
        var refundTotal = amount?.ToMinorUnits() ?? remainingTotalMinor;
        if (amount is not null && amount.Value.Currency != escrow.Currency)
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.CurrencyMismatch());
        if (refundTotal <= 0)
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.AmountMustBePositive());
        if (refundTotal > remainingTotalMinor)
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.AmountExceedsRemaining());

        var refundedGrossMinor = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.GrossRefundedMinor);
        var remainingGrossMinor = checked(escrow.PayeeGrossMinor - refundedGrossMinor);
        var grossRefundMinor = Math.Min(refundTotal, remainingGrossMinor);
        var commissionRefundMinor = checked(refundTotal - grossRefundMinor);
        return await ExecuteRefundAsync(escrow, grossRefundMinor, commissionRefundMinor, 0, reason, ct);
    }

    public async Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowToRefundForBooking(bookingId);
            return Result<Option<Refund>, EscrowRefundError>.Success(Option.None<Refund>());
        }

        if (escrow.Status == EscrowStatus.Refunded)
        {
            logger.EscrowAlreadyRefunded(escrow.Id, bookingId);
            return Result<Option<Refund>, EscrowRefundError>.Success(Option.None<Refund>());
        }

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
        {
            logger.EscrowNotRefundableSkippingRefund(escrow.Id, bookingId, escrow.Status);
            return Result<Option<Refund>, EscrowRefundError>.Success(Option.None<Refund>());
        }

        var refund = await RefundAsync(escrow.Id, amount, reason, ct);
        if (!refund.TryGetValue(out var completedRefund))
        {
            refund.TryGetError(out var error);
            return Result<Option<Refund>, EscrowRefundError>.Failure(error!);
        }
        return Result<Option<Refund>, EscrowRefundError>.Success(Option.Some(completedRefund));
    }

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

    private async Task<Result<Refund, EscrowRefundError>> ExecuteRefundAsync(
        EscrowEntity escrow,
        long grossRefundMinor,
        long commissionRefundMinor,
        long commissionVatReversedMinor,
        string? reason,
        CancellationToken ct)
    {
        var payerTotalRefundMinor = checked(grossRefundMinor + commissionRefundMinor);

        if (!await escrowRepository.TryReserveRefundGrossAsync(escrow.Id, grossRefundMinor, ct))
            return await ReservationConflictAsync(escrow.Id, grossRefundMinor, ct);

        var reservation = PaymentRefundEntity.CreatePendingForEscrow(
            escrow.Id,
            grossRefundMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            timeProvider.GetUtcNow());
        if (escrow.RecordRefund(reservation).IsFailure)
        {
            await escrowRepository.ReleaseReservedRefundGrossAsync(escrow.Id, grossRefundMinor, ct);
            throw new InvalidOperationException("Escrow refund reservation could not be recorded.");
        }

        await unitOfWork.SaveChangesAsync(ct);

        var cumulativeGrossRefundMinor = escrow.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.GrossRefundedMinor);
        var metadata = EscrowMetadata(escrow, TransactionTypes.EscrowRefund);
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
            Metadata = metadata
        }, ct);
        if (!refund.TryGetValue(out var completedRefund))
        {
            if (escrow.ReleaseRefund(reservation).IsFailure)
                throw new InvalidOperationException("Escrow refund reservation could not be released.");
            await unitOfWork.SaveChangesAsync(ct);
            await escrowRepository.ReleaseReservedRefundGrossAsync(escrow.Id, grossRefundMinor, ct);
            refund.TryGetError(out var error);
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.PaymentFailure(error!));
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
        await ledger.StageAsync(refundPosting, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<Refund, EscrowRefundError>.Success(completedRefund);
    }

    private async Task<Result<Refund, EscrowRefundError>> ReservationConflictAsync(
        int escrowId,
        long grossRefundMinor,
        CancellationToken ct)
    {
        var current = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct);
        if (current is null)
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.EscrowNotFound());
        if (current.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.EscrowNotRefundable());
        return checked(current.RefundedGrossMinor + grossRefundMinor) > current.PayeeGrossMinor
            ? Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.AmountExceedsRemaining())
            : Result<Refund, EscrowRefundError>.Failure(new EscrowRefundError.Conflict());
    }

    private async Task<Option<PaymentError>> ValidatePayerAsync(
        Guid payerId,
        PaymentSession session,
        CancellationToken ct)
    {
        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return Option.Some<PaymentError>(new PaymentError.PayerNotFound());
        return session == PaymentSession.OffSession && payer.StripeCustomerId is null
            ? Option.Some<PaymentError>(new PaymentError.PayerUnavailable())
            : Option.None<PaymentError>();
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
