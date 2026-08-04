using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Concertable.Payment.Domain;
using Concertable.Kernel.ValueObjects;

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
    private readonly TimeProvider timeProvider;
    private readonly ILogger<EscrowService> logger;
    private readonly Money platformFee;

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

    public async Task<Result<EscrowDeposit, DepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.PaymentFailure(new PaymentError.PayerNotFound()));

        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.PaymentFailure(new PaymentError.PayerUnavailable()));

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
            if (!hold.TryGetError(out var error))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.PaymentFailure(error));
        }

        if (string.IsNullOrEmpty(outcome.TransactionId))
            throw new InvalidOperationException("Stripe hold response missing PaymentIntent id.");

        var escrow = EscrowEntity.Create(
            bookingId,
            payerId,
            payeeId,
            amount,
            platformFee,
            outcome.TransactionId);

        await escrowRepository.AddAsync(escrow);
        await unitOfWork.SaveChangesAsync(ct);

        if (!outcome.RequiresAction)
        {
            escrow.Confirm();
            await ledger.StageAsync(
                LedgerPostings.EscrowHold(
                    escrow.FromOwnerId,
                    escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                    escrow.BookingId,
                    escrow.ChargeId),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Result.Success<EscrowDeposit, DepositError>(new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, outcome.ClientSecret));
    }

    public async Task<Result<EscrowDeposit, DepositError>> DepositBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var existing = await escrowRepository.GetByCommissionBindingIdAsync(
            commissionBindingId,
            ct);
        if (existing is not null)
            return Result.Success<EscrowDeposit, DepositError>(new EscrowDeposit(
                existing.Id,
                existing.ChargeId,
                existing.Status));

        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            currency,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor,
            null,
            stripeSetupIntentId,
            ct);
        if (!authorized.TryGetValue(out var boundCommission))
        {
            if (!authorized.TryGetError(out var commissionError))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.CommissionFailure(commissionError));
        }

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.PaymentFailure(new PaymentError.PayerNotFound()));
        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.PaymentFailure(new PaymentError.PayerUnavailable()));

        var calculation = boundCommission.Calculation;
        var hold = await paymentManager.HoldAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(boundCommission, bookingId, TransactionTypes.Escrow),
            ct);
        if (!hold.TryGetValue(out var outcome))
        {
            if (!hold.TryGetError(out var paymentError))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<EscrowDeposit, DepositError>(new DepositError.PaymentFailure(paymentError));
        }
        if (string.IsNullOrEmpty(outcome.TransactionId))
            throw new InvalidOperationException("Stripe hold response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(
            boundCommission.Binding,
            outcome.TransactionId);
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
            escrow.Confirm();
            await ledger.StageAsync(
                LedgerPostings.EscrowHold(
                    escrow.FromOwnerId,
                    escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                    escrow.BookingId,
                    escrow.ChargeId),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success<EscrowDeposit, DepositError>(new EscrowDeposit(
            escrow.Id,
            escrow.ChargeId,
            escrow.Status,
            outcome.ClientSecret));
    }

    public async Task<Result<EscrowDeposit, CaptureError>> CaptureAsync(
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
            return Result.Failure<EscrowDeposit, CaptureError>(error);

        var escrow = EscrowEntity.Create(bookingId, payerId, payeeId, amount, platformFee, paymentIntentId);
        escrow.Confirm();
        await escrowRepository.AddAsync(escrow);
        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success<EscrowDeposit, CaptureError>(new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, null));
    }

    public async Task<Result<EscrowDeposit, CaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        CancellationToken ct = default)
    {
        var existing = await escrowRepository.GetByCommissionBindingIdAsync(
            commissionBindingId,
            ct);
        if (existing is not null)
            return Result.Success<EscrowDeposit, CaptureError>(new EscrowDeposit(
                existing.Id,
                existing.ChargeId,
                existing.Status));

        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            currency,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor,
            paymentIntentId,
            null,
            ct);
        if (!authorized.TryGetValue(out var boundCommission))
        {
            if (!authorized.TryGetError(out var commissionError))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<EscrowDeposit, CaptureError>(new CaptureError.CommissionFailure(commissionError));
        }

        var capture = await paymentManager.CaptureAsync(new CaptureRequest
        {
            PaymentIntentId = paymentIntentId,
            Metadata = CommissionMetadata(
                boundCommission,
                bookingId,
                TransactionTypes.Escrow)
        }, ct);
        if (capture.TryGetError(out var captureError))
            return Result.Failure<EscrowDeposit, CaptureError>(captureError);

        commissionService.BindPaymentIntent(
            boundCommission.Binding,
            paymentIntentId);
        var escrow = EscrowEntity.CreateBound(
            bookingId,
            payerId,
            payeeId,
            commissionBindingId,
            boundCommission.Calculation,
            paymentIntentId);
        escrow.Confirm();
        await escrowRepository.AddAsync(escrow, ct);
        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success<EscrowDeposit, CaptureError>(new EscrowDeposit(
            escrow.Id,
            escrow.ChargeId,
            escrow.Status));
    }

    public async Task<Result<Transfer, ReleaseError>> ReleaseAsync(int escrowId, CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByIdAsync(escrowId);
        if (escrow is null)
            return Result.Failure<Transfer, ReleaseError>(ReleaseError.EscrowNotFound);

        if (escrow.Status != EscrowStatus.Held)
            return Result.Failure<Transfer, ReleaseError>(ReleaseError.InvalidEscrowState);

        var release = await paymentManager.ReleaseAsync(new ReleaseRequest
        {
            PayeeId = escrow.ToOwnerId,
            Amount = escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
            ChargeId = escrow.ChargeId,
            Metadata = EscrowMetadata(escrow, TransactionTypes.EscrowRelease)
        }, ct);

        if (!release.TryGetValue(out var transfer))
            return release;

        escrow.Release(transfer.TransferId, timeProvider.GetUtcNow().DateTime);
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

        return release;
    }

    public async Task<Result<Option<Transfer>, ReleaseError>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowFoundForBooking(bookingId);
            return Result.Success<Option<Transfer>, ReleaseError>(Option.None<Transfer>());
        }

        if (escrow.Status != EscrowStatus.Held)
        {
            logger.EscrowNotHeldSkippingRelease(escrow.Id, bookingId, escrow.Status);
            return Result.Success<Option<Transfer>, ReleaseError>(Option.None<Transfer>());
        }

        var release = await ReleaseAsync(escrow.Id, ct);
        if (!release.TryGetValue(out var transfer))
        {
            if (!release.TryGetError(out var error))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<Option<Transfer>, ReleaseError>(error);
        }
        return Result.Success<Option<Transfer>, ReleaseError>(Option.Some(transfer));
    }

    public async Task<Result<Refund, RefundError>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct);
        if (escrow is null)
            return Result.Failure<Refund, RefundError>(RefundError.EscrowNotFound);

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result.Failure<Refund, RefundError>(RefundError.InvalidEscrowState);

        var refundedTotalMinor = escrow.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.PayerTotalRefundedMinor);
        var remainingTotalMinor = checked(escrow.PayerTotalMinor - refundedTotalMinor);
        var refundTotal = amount?.ToMinorUnits() ?? remainingTotalMinor;
        if (amount is not null && amount.Value.Currency != escrow.Currency)
            return Result.Failure<Refund, RefundError>(RefundError.CurrencyMismatch);
        if (refundTotal <= 0 || refundTotal > remainingTotalMinor)
            return Result.Failure<Refund, RefundError>(RefundError.InvalidAmount);

        var refundedGrossMinor = escrow.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.GrossRefundedMinor);
        var remainingGrossMinor = checked(escrow.PayeeGrossMinor - refundedGrossMinor);
        var grossRefundMinor = Math.Min(refundTotal, remainingGrossMinor);
        var commissionRefundMinor = checked(refundTotal - grossRefundMinor);

        return await ExecuteRefundAsync(
            escrow,
            grossRefundMinor,
            commissionRefundMinor,
            commissionVatReversedMinor: 0,
            reason,
            ct);
    }

    public async Task<Result<Option<Refund>, RefundError>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowToRefundForBooking(bookingId);
            return Result.Success<Option<Refund>, RefundError>(Option.None<Refund>());
        }

        if (escrow.Status == EscrowStatus.Refunded)
        {
            logger.EscrowAlreadyRefunded(escrow.Id, bookingId);
            return Result.Success<Option<Refund>, RefundError>(Option.Some(new Refund(
                escrow.Refunds
                    .Where(r => r.Status == PaymentRefundStatus.Completed)
                    .OrderByDescending(r => r.CompletedAt)
                    .First()
                    .StripeRefundId!)));
        }

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
        {
            logger.EscrowNotRefundableSkippingRefund(escrow.Id, bookingId, escrow.Status);
            return Result.Success<Option<Refund>, RefundError>(Option.None<Refund>());
        }

        var refund = await RefundAsync(escrow.Id, amount, reason, ct);
        if (!refund.TryGetValue(out var refundValue))
        {
            if (!refund.TryGetError(out var error))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<Option<Refund>, RefundError>(error);
        }
        return Result.Success<Option<Refund>, RefundError>(Option.Some(refundValue));
    }

    public async Task<Result<Option<Refund>, RefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowToRefundForBooking(bookingId);
            return Result.Success<Option<Refund>, RefundError>(Option.None<Refund>());
        }

        if (escrow.CommissionBindingId is null)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.CommissionBindingNotFound);
        if (currency != escrow.Currency)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.CurrencyMismatch);
        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidEscrowState);
        if (grossMinor <= 0)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidAmount);

        var grossAlreadyRefunded = escrow.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.GrossRefundedMinor);
        var cumulativeGrossRefund = checked(grossAlreadyRefunded + grossMinor);
        if (cumulativeGrossRefund > escrow.PayeeGrossMinor)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidAmount);

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
            escrow.Refunds.Where(r => r.CountsTowardCumulative).Sum(r => r.CommissionRefundedMinor));
        var commissionVatReversedMinor = checked(
            cumulativeVatReversal -
            escrow.Refunds.Where(r => r.CountsTowardCumulative).Sum(r => r.CommissionVatReversedMinor));

        var refund = await ExecuteRefundAsync(
            escrow,
            grossMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            reason,
            ct);
        if (!refund.TryGetValue(out var refundValue))
        {
            if (!refund.TryGetError(out var error))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<Option<Refund>, RefundError>(error);
        }
        return Result.Success<Option<Refund>, RefundError>(Option.Some(refundValue));
    }

    public async Task<EscrowDto?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
            return null;

        return new EscrowDto(
            escrow.Id,
            escrow.BookingId,
            escrow.FromOwnerId,
            escrow.ToOwnerId,
            escrow.PayerTotalMinor.ToMoney(escrow.Currency).Amount,
            escrow.Status,
            escrow.ChargeId,
            escrow.TransferId,
            escrow.ReleasedAt);
    }

    private async Task<Result<Refund, RefundError>> ExecuteRefundAsync(
        EscrowEntity escrow,
        long grossRefundMinor,
        long commissionRefundMinor,
        long commissionVatReversedMinor,
        string? reason,
        CancellationToken ct)
    {
        var payerTotalRefundMinor = checked(grossRefundMinor + commissionRefundMinor);

        if (!await escrowRepository.TryReserveRefundGrossAsync(escrow.Id, grossRefundMinor, ct))
            return Result.Failure<Refund, RefundError>(RefundError.InvalidAmount);

        var reservation = PaymentRefundEntity.CreatePendingForEscrow(
            escrow.Id,
            grossRefundMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            timeProvider.GetUtcNow());
        escrow.RecordRefund(reservation);
        await unitOfWork.SaveChangesAsync(ct);

        var cumulativeGrossRefundMinor = escrow.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.GrossRefundedMinor);
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
                : new TransferReversal(
                    escrow.TransferId,
                    grossRefundMinor.ToMoney(escrow.Currency)),
            Reason = reason,
            Metadata = metadata
        }, ct);
        if (!refund.TryGetValue(out var refundValue))
        {
            escrow.ReleaseRefund(reservation);
            await unitOfWork.SaveChangesAsync(ct);
            await escrowRepository.ReleaseReservedRefundGrossAsync(escrow.Id, grossRefundMinor, ct);
            return refund;
        }

        escrow.CompleteRefund(reservation, refundValue.RefundId, timeProvider.GetUtcNow());

        var refundPosting = escrow.TransferId is null
            ? LedgerPostings.EscrowRefundBeforeRelease(
                escrow.FromOwnerId,
                payerTotalRefundMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                refundValue.RefundId)
            : LedgerPostings.EscrowRefundAfterRelease(
                escrow.FromOwnerId,
                escrow.ToOwnerId,
                grossRefundMinor.ToMoney(escrow.Currency),
                checked(commissionRefundMinor - commissionVatReversedMinor).ToMoney(escrow.Currency),
                commissionVatReversedMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                refundValue.RefundId);
        await ledger.StageAsync(refundPosting, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return refund;
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
            metadata[PaymentMetadataKeys.CommissionBindingId] =
                escrow.CommissionBindingId.Value.ToString();

        return metadata;
    }

}
