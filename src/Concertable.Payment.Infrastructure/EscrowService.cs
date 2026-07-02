using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Exceptions;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure;

internal sealed class EscrowService : IEscrowService
{
    private readonly IPaymentManager paymentManager;
    private readonly IEscrowRepository escrowRepository;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<EscrowService> logger;

    public EscrowService(
        IPaymentManager paymentManager,
        IEscrowRepository escrowRepository,
        IPayoutAccountRepository payoutAccountRepository,
        TimeProvider timeProvider,
        ILogger<EscrowService> logger)
    {
        this.paymentManager = paymentManager;
        this.escrowRepository = escrowRepository;
        this.payoutAccountRepository = payoutAccountRepository;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<Result<EscrowDeposit>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            throw new BadRequestException("Stripe customer setup is required for off-session payments.");

        var hold = await paymentManager.HoldAsync(new HoldRequest
        {
            PayerId = payerId,
            PayerEmail = payer.Email,
            PayeeId = payeeId,
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = TransactionTypes.Escrow,
                ["bookingId"] = bookingId.ToString()
            },
            Session = session
        }, ct);

        if (hold.IsFailed)
            return hold.ToResult<EscrowDeposit>();

        if (string.IsNullOrEmpty(hold.Value.TransactionId))
            return Result.Fail("Stripe hold response missing PaymentIntent id.");

        var escrow = EscrowEntity.Create(
            bookingId,
            payerId,
            payeeId,
            (long)(amount * 100),
            hold.Value.TransactionId);

        await escrowRepository.AddAsync(escrow);
        await escrowRepository.SaveChangesAsync();

        if (!hold.Value.RequiresAction)
        {
            escrow.Confirm();
            await escrowRepository.SaveChangesAsync();
        }

        return Result.Ok(new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, hold.Value.ClientSecret));
    }

    public async Task<Result<EscrowDeposit>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default)
    {
        var capture = await paymentManager.CaptureAsync(new CaptureRequest
        {
            PaymentIntentId = paymentIntentId,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = TransactionTypes.Escrow,
                ["bookingId"] = bookingId.ToString()
            }
        }, ct);

        if (capture.IsFailed)
            return capture.ToResult<EscrowDeposit>();

        var escrow = EscrowEntity.Create(bookingId, payerId, payeeId, (long)(amount * 100), paymentIntentId);
        escrow.Confirm();
        await escrowRepository.AddAsync(escrow);
        await escrowRepository.SaveChangesAsync();

        return Result.Ok(new EscrowDeposit(escrow.Id, escrow.ChargeId, escrow.Status, null));
    }

    public async Task<Result<Transfer>> ReleaseAsync(int escrowId, CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByIdAsync(escrowId)
            ?? throw new NotFoundException($"Escrow {escrowId} not found");

        if (escrow.Status != EscrowStatus.Held)
            return Result.Fail($"Escrow {escrowId} is {escrow.Status}, not Held");

        var release = await paymentManager.ReleaseAsync(new ReleaseRequest
        {
            PayeeId = escrow.ToOwnerId,
            Amount = escrow.Amount / 100m,
            ChargeId = escrow.ChargeId,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "escrowRelease",
                ["escrowId"] = escrow.Id.ToString(),
                ["bookingId"] = escrow.BookingId.ToString()
            }
        }, ct);

        if (release.IsFailed)
            return release;

        escrow.Release(release.Value.TransferId, timeProvider.GetUtcNow().DateTime);
        await escrowRepository.SaveChangesAsync();

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
        decimal? amount = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var escrow = await escrowRepository.GetByIdAsync(escrowId)
            ?? throw new NotFoundException($"Escrow {escrowId} not found");

        if (escrow.Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return Result.Fail($"Escrow {escrowId} is {escrow.Status}; cannot refund");

        var refundAmount = amount ?? escrow.Amount / 100m;

        var refund = await paymentManager.RefundAsync(new RefundRequest
        {
            Amount = refundAmount,
            PaymentIntentId = escrow.ChargeId,
            TransferId = escrow.TransferId,
            Reason = reason,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "escrowRefund",
                ["escrowId"] = escrow.Id.ToString(),
                ["bookingId"] = escrow.BookingId.ToString()
            }
        }, ct);

        if (refund.IsFailed)
            return refund;

        escrow.Refund(refund.Value.RefundId, timeProvider.GetUtcNow().DateTime);
        await escrowRepository.SaveChangesAsync();

        return refund;
    }

    public async Task<Result<Refund?>> RefundByBookingIdAsync(
        int bookingId,
        decimal? amount = null,
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
            return Result.Ok<Refund?>(new Refund(escrow.RefundId!));
        }

        var refund = await RefundAsync(escrow.Id, amount, reason, ct);
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
            escrow.Amount / 100m,
            escrow.Status,
            escrow.ChargeId,
            escrow.TransferId,
            escrow.RefundId,
            escrow.ReleasedAt,
            escrow.RefundedAt);
    }
}
