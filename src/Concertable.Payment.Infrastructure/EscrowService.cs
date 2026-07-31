using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Exceptions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
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
    private readonly PaymentDbContext context;
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
        PaymentDbContext context,
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
        this.context = context;
        this.platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<Result<EscrowDeposit>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            throw new BadRequestException("Stripe customer setup is required for off-session payments.");

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

        if (hold.IsFailed)
            return hold.ToResult<EscrowDeposit>();

        if (string.IsNullOrEmpty(hold.Value.TransactionId))
            return Result.Fail("Stripe hold response missing PaymentIntent id.");

        var escrow = EscrowEntity.Create(
            bookingId,
            payerId,
            payeeId,
            amount,
            platformFee,
            hold.Value.TransactionId);

        await escrowRepository.AddAsync(escrow);
        await unitOfWork.SaveChangesAsync(ct);

        if (!hold.Value.RequiresAction)
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

        return Result.Ok(new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, hold.Value.ClientSecret));
    }

    public async Task<Result<EscrowDeposit>> DepositBoundCommissionAsync(
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
            return Result.Ok(new EscrowDeposit(
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
        if (authorized.IsFailed)
            return authorized.ToResult<EscrowDeposit>();

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");
        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            throw new BadRequestException("Stripe customer setup is required for off-session payments.");

        var calculation = authorized.Value.Calculation;
        var hold = await paymentManager.HoldAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(authorized.Value, bookingId, TransactionTypes.Escrow),
            ct);
        if (hold.IsFailed)
            return hold.ToResult<EscrowDeposit>();
        if (string.IsNullOrEmpty(hold.Value.TransactionId))
            return Result.Fail("Stripe hold response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(
            authorized.Value.Binding,
            hold.Value.TransactionId);
        var escrow = EscrowEntity.CreateBound(
            bookingId,
            payerId,
            payeeId,
            commissionBindingId,
            calculation,
            hold.Value.TransactionId);
        await escrowRepository.AddAsync(escrow, ct);

        if (!hold.Value.RequiresAction)
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
        return Result.Ok(new EscrowDeposit(
            escrow.Id,
            escrow.ChargeId,
            escrow.Status,
            hold.Value.ClientSecret));
    }

    public async Task<Result<EscrowDeposit>> CaptureAsync(
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

        if (capture.IsFailed)
            return capture.ToResult<EscrowDeposit>();

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

        return Result.Ok(new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, null));
    }

    public async Task<Result<EscrowDeposit>> CaptureBoundCommissionAsync(
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
            return Result.Ok(new EscrowDeposit(
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
        if (authorized.IsFailed)
            return authorized.ToResult<EscrowDeposit>();

        var capture = await paymentManager.CaptureAsync(new CaptureRequest
        {
            PaymentIntentId = paymentIntentId,
            Metadata = CommissionMetadata(
                authorized.Value,
                bookingId,
                TransactionTypes.Escrow)
        }, ct);
        if (capture.IsFailed)
            return capture.ToResult<EscrowDeposit>();

        commissionService.BindPaymentIntent(
            authorized.Value.Binding,
            paymentIntentId);
        var escrow = EscrowEntity.CreateBound(
            bookingId,
            payerId,
            payeeId,
            commissionBindingId,
            authorized.Value.Calculation,
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

        return Result.Ok(new EscrowDeposit(
            escrow.Id,
            escrow.ChargeId,
            escrow.Status));
    }

    public async Task<Result<Transfer>> ReleaseAsync(int escrowId, CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByIdAsync(escrowId)
            .OrNotFound($"Escrow {escrowId}");

        if (escrow.Status != EscrowStatus.Held)
            return Result.Fail($"Escrow {escrowId} is {escrow.Status}, not Held");

        var release = await paymentManager.ReleaseAsync(new ReleaseRequest
        {
            PayeeId = escrow.ToOwnerId,
            Amount = escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
            ChargeId = escrow.ChargeId,
            Metadata = EscrowMetadata(escrow, TransactionTypes.EscrowRelease)
        }, ct);

        if (release.IsFailed)
            return release;

        escrow.Release(release.Value.TransferId, timeProvider.GetUtcNow().DateTime);
        await ledger.StageAsync(
            LedgerPostings.EscrowRelease(
                escrow.ToOwnerId,
                escrow.PayeeGrossMinor.ToMoney(escrow.Currency),
                escrow.CommissionNetMinor.ToMoney(escrow.Currency),
                escrow.CommissionVatMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                release.Value.TransferId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return release;
    }

    public async Task<Result<Transfer?>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowFoundForBooking(bookingId);
            return Result.Ok<Transfer?>(null);
        }

        if (escrow.Status != EscrowStatus.Held)
        {
            logger.EscrowNotHeldSkippingRelease(escrow.Id, bookingId, escrow.Status);
            return Result.Ok<Transfer?>(null);
        }

        var release = await ReleaseAsync(escrow.Id, ct);
        return release.IsFailed
            ? release.ToResult<Transfer?>()
            : Result.Ok<Transfer?>(release.Value);
    }

    public async Task<Result<Refund>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct)
            .OrNotFound($"Escrow {escrowId}");

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result.Fail($"Escrow {escrowId} is {escrow.Status}; cannot refund");

        var refundedTotalMinor = escrow.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.PayerTotalRefundedMinor);
        var remainingTotalMinor = checked(escrow.PayerTotalMinor - refundedTotalMinor);
        var refundTotal = amount?.ToMinorUnits() ?? remainingTotalMinor;
        if (amount is not null && amount.Value.Currency != escrow.Currency)
            return Result.Fail("currency_mismatch");
        if (refundTotal <= 0 || refundTotal > remainingTotalMinor)
            return Result.Fail("refund_amount_exceeds_remaining_total");

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

    public async Task<Result<Refund?>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByBookingIdAsync(bookingId, ct);
        if (escrow is null)
        {
            logger.NoEscrowToRefundForBooking(bookingId);
            return Result.Ok<Refund?>(null);
        }

        if (escrow.Status == EscrowStatus.Refunded)
        {
            logger.EscrowAlreadyRefunded(escrow.Id, bookingId);
            return Result.Ok<Refund?>(new Refund(
                escrow.Refunds
                    .Where(r => r.Status == PaymentRefundStatus.Completed)
                    .OrderByDescending(r => r.CompletedAt)
                    .First()
                    .StripeRefundId!));
        }

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
        {
            logger.EscrowNotRefundableSkippingRefund(escrow.Id, bookingId, escrow.Status);
            return Result.Ok<Refund?>(null);
        }

        var refund = await RefundAsync(escrow.Id, amount, reason, ct);
        return refund.IsFailed
            ? refund.ToResult<Refund?>()
            : Result.Ok<Refund?>(refund.Value);
    }

    public async Task<Result<Refund?>> RefundBoundCommissionByBookingIdAsync(
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
            return Result.Ok<Refund?>(null);
        }

        if (escrow.CommissionBindingId is null)
            return Result.Fail("commission_binding_not_found");
        if (currency != escrow.Currency)
            return Result.Fail("currency_mismatch");
        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result.Fail($"Escrow {escrow.Id} is {escrow.Status}; cannot refund");
        if (grossMinor <= 0)
            return Result.Fail("refund_gross_must_be_positive");

        var grossAlreadyRefunded = escrow.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.GrossRefundedMinor);
        var cumulativeGrossRefund = checked(grossAlreadyRefunded + grossMinor);
        if (cumulativeGrossRefund > escrow.PayeeGrossMinor)
            return Result.Fail("refund_gross_exceeds_remaining_gross");

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
        return refund.IsFailed
            ? refund.ToResult<Refund?>()
            : Result.Ok<Refund?>(refund.Value);
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

    private async Task<Result<Refund>> ExecuteRefundAsync(
        EscrowEntity escrow,
        long grossRefundMinor,
        long commissionRefundMinor,
        long commissionVatReversedMinor,
        string? reason,
        CancellationToken ct)
    {
        var payerTotalRefundMinor = checked(grossRefundMinor + commissionRefundMinor);

        var reservation = PaymentRefundEntity.CreatePendingForEscrow(
            escrow.Id,
            grossRefundMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            timeProvider.GetUtcNow());
        escrow.RecordRefund(reservation);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return await ReservationConflictAsync(escrow.Id, grossRefundMinor, ct);
        }

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
        if (refund.IsFailed)
        {
            escrow.ReleaseRefund(reservation);
            await unitOfWork.SaveChangesAsync(ct);
            return refund;
        }

        escrow.CompleteRefund(reservation, refund.Value.RefundId, timeProvider.GetUtcNow());

        var refundPosting = escrow.TransferId is null
            ? LedgerPostings.EscrowRefundBeforeRelease(
                escrow.FromOwnerId,
                payerTotalRefundMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                refund.Value.RefundId)
            : LedgerPostings.EscrowRefundAfterRelease(
                escrow.FromOwnerId,
                escrow.ToOwnerId,
                grossRefundMinor.ToMoney(escrow.Currency),
                checked(commissionRefundMinor - commissionVatReversedMinor).ToMoney(escrow.Currency),
                commissionVatReversedMinor.ToMoney(escrow.Currency),
                escrow.BookingId,
                escrow.ChargeId,
                refund.Value.RefundId);
        await ledger.StageAsync(refundPosting, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return refund;
    }

    private async Task<Result<Refund>> ReservationConflictAsync(
        int escrowId,
        long grossRefundMinor,
        CancellationToken ct)
    {
        var current = await escrowRepository.GetWithRefundsByIdAsync(escrowId, ct);
        var reservedGross = current?.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.GrossRefundedMinor) ?? 0;
        return checked(reservedGross + grossRefundMinor) > (current?.PayeeGrossMinor ?? 0)
            ? Result.Fail("refund_gross_exceeds_remaining_gross")
            : Result.Fail("refund_conflict");
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
