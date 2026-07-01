using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Kernel.Exceptions;
using FluentResults;
using Microsoft.Extensions.Logging;
using Stripe;
using Transfer = Concertable.Payment.Application.DTOs.Transfer;
using Refund = Concertable.Payment.Application.DTOs.Refund;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentManager : IPaymentManager
{
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly IStripePaymentIntentClientFactory intentClientFactory;
    private readonly IStripeTransferClient transferClient;
    private readonly IStripeHoldClient stripeHoldClient;
    private readonly ILogger<PaymentManager> logger;

    public PaymentManager(
        IPayoutAccountRepository payoutAccountRepository,
        IStripePaymentIntentClientFactory intentClientFactory,
        IStripeTransferClient transferClient,
        IStripeHoldClient stripeHoldClient,
        ILogger<PaymentManager> logger)
    {
        this.payoutAccountRepository = payoutAccountRepository;
        this.intentClientFactory = intentClientFactory;
        this.transferClient = transferClient;
        this.stripeHoldClient = stripeHoldClient;
        this.logger = logger;
    }

    public async Task<Result<PaymentOutcome>> ChargeAsync(ChargeRequest r, CancellationToken ct = default)
    {
        var payerAccount = await payoutAccountRepository.GetByOwnerIdAsync(r.PayerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {r.PayerId}");
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(r.PayeeId, ct)
            ?? throw new NotFoundException($"Payout account not found for payee {r.PayeeId}");

        var stripeCustomerId = payerAccount.StripeCustomerId
            ?? throw new BadRequestException("Payer has no Stripe customer ID");
        var destinationStripeId = payeeAccount.StripeAccountId
            ?? throw new BadRequestException("Payee has no Stripe Connect account");

        var metadata = new Dictionary<string, string>
        {
            ["fromUserId"] = r.PayerId.ToString(),
            ["fromUserEmail"] = r.PayerEmail,
            ["toUserId"] = r.PayeeId.ToString(),
            ["amount"] = ((long)(r.Amount * 100)).ToString()
        }
        .Merge(r.Metadata);

        logger.ChargingPayment(r.PayerId, r.Amount, r.PayeeId, destinationStripeId, r.Metadata["type"]);

        return await intentClientFactory.Create(r.Session).ChargeAsync(new StripeChargeOptions
        {
            Amount = r.Amount,
            PaymentMethodId = r.PaymentMethodId,
            StripeCustomerId = stripeCustomerId,
            DestinationStripeId = destinationStripeId,
            ReceiptEmail = r.PayerEmail,
            Metadata = metadata
        });
    }

    public async Task<Result<PaymentOutcome>> HoldAsync(HoldRequest r, CancellationToken ct = default)
    {
        var payerAccount = await payoutAccountRepository.GetByOwnerIdAsync(r.PayerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {r.PayerId}");
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(r.PayeeId, ct)
            ?? throw new NotFoundException($"Payout account not found for payee {r.PayeeId}");

        var stripeCustomerId = payerAccount.StripeCustomerId
            ?? throw new BadRequestException("Payer has no Stripe customer ID");
        var destinationStripeId = payeeAccount.StripeAccountId
            ?? throw new BadRequestException("Payee has no Stripe Connect account");

        var metadata = new Dictionary<string, string>
        {
            ["fromUserId"] = r.PayerId.ToString(),
            ["fromUserEmail"] = r.PayerEmail,
            ["toUserId"] = r.PayeeId.ToString(),
            ["amount"] = ((long)(r.Amount * 100)).ToString()
        }
        .Merge(r.Metadata);

        logger.HoldingPayment(r.Amount, r.PayerId, r.PayeeId, destinationStripeId, r.Metadata["type"]);

        return await intentClientFactory.Create(r.Session).HoldAsync(new StripeHoldOptions
        {
            Amount = r.Amount,
            PaymentMethodId = r.PaymentMethodId,
            StripeCustomerId = stripeCustomerId,
            DestinationStripeId = destinationStripeId,
            ReceiptEmail = r.PayerEmail,
            Metadata = metadata
        });
    }

    public async Task<Result<Transfer>> ReleaseAsync(ReleaseRequest r, CancellationToken ct = default)
    {
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(r.PayeeId, ct)
            ?? throw new NotFoundException($"Payout account not found for payee {r.PayeeId}");

        var destinationStripeId = payeeAccount.StripeAccountId
            ?? throw new BadRequestException("Payee has no Stripe Connect account");

        var metadata = new Dictionary<string, string>
        {
            ["toUserId"] = r.PayeeId.ToString(),
            ["amount"] = ((long)(r.Amount * 100)).ToString()
        }
        .Merge(r.Metadata);

        logger.ReleasingPayment(r.Amount, r.PayeeId, destinationStripeId, r.ChargeId);

        return await transferClient.ReleaseAsync(new StripeReleaseOptions
        {
            Amount = r.Amount,
            ChargeId = r.ChargeId,
            DestinationStripeId = destinationStripeId,
            Metadata = metadata
        });
    }

    public async Task<Result<Refund>> RefundAsync(RefundRequest r, CancellationToken ct = default)
    {
        var metadata = new Dictionary<string, string>
        {
            ["amount"] = ((long)(r.Amount * 100)).ToString()
        }
        .Merge(r.Metadata);

        logger.RefundingPayment(r.Amount, r.PaymentIntentId, string.IsNullOrEmpty(r.TransferId) ? string.Empty : $" (reversing transfer {r.TransferId})");

        return await transferClient.RefundAsync(new StripeRefundOptions
        {
            Amount = r.Amount,
            PaymentIntentId = r.PaymentIntentId,
            TransferId = r.TransferId,
            Reason = r.Reason,
            Metadata = metadata
        });
    }

    public async Task<Result> CaptureAsync(CaptureRequest r, CancellationToken ct = default)
    {
        try
        {
            logger.CapturingPaymentIntent(r.PaymentIntentId, r.Metadata["type"]);

            await stripeHoldClient.CaptureAsync(r.PaymentIntentId, r.Metadata, ct);
            return Result.Ok();
        }
        catch (StripeException ex)
        {
            logger.StripeCaptureFailedForPaymentIntent(r.PaymentIntentId, ex.StripeError?.Code, ex);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.CaptureFailedForPaymentIntent(r.PaymentIntentId, ex);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }
}
