using Concertable.Shared.Exceptions;
using FluentResults;
using Microsoft.Extensions.Logging;
using Stripe;

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

    public async Task<Result<PaymentResponse>> ChargeAsync(ChargeRequest r, CancellationToken ct = default)
    {
        var payerAccount = await payoutAccountRepository.GetByUserIdAsync(r.PayerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {r.PayerId}");
        var payeeAccount = await payoutAccountRepository.GetByUserIdAsync(r.PayeeId, ct)
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

        logger.LogInformation(
            "Charging {PayerId} {Amount} GBP -> {PayeeId} (stripe {DestinationStripeId}) for {Purpose}",
            r.PayerId, r.Amount, r.PayeeId, destinationStripeId, r.Metadata["type"]);

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

    public async Task<Result<PaymentResponse>> HoldAsync(HoldRequest r, CancellationToken ct = default)
    {
        var payerAccount = await payoutAccountRepository.GetByUserIdAsync(r.PayerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {r.PayerId}");
        var payeeAccount = await payoutAccountRepository.GetByUserIdAsync(r.PayeeId, ct)
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

        logger.LogInformation(
            "Holding {Amount} GBP from {PayerId} on behalf of {PayeeId} (stripe {DestinationStripeId}) for {Purpose}",
            r.Amount, r.PayerId, r.PayeeId, destinationStripeId, r.Metadata["type"]);

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

    public async Task<Result<TransferResponse>> ReleaseAsync(ReleaseRequest r, CancellationToken ct = default)
    {
        var payeeAccount = await payoutAccountRepository.GetByUserIdAsync(r.PayeeId, ct)
            ?? throw new NotFoundException($"Payout account not found for payee {r.PayeeId}");

        var destinationStripeId = payeeAccount.StripeAccountId
            ?? throw new BadRequestException("Payee has no Stripe Connect account");

        var metadata = new Dictionary<string, string>
        {
            ["toUserId"] = r.PayeeId.ToString(),
            ["amount"] = ((long)(r.Amount * 100)).ToString()
        }
        .Merge(r.Metadata);

        logger.LogInformation(
            "Releasing {Amount} GBP from platform balance to {PayeeId} (stripe {DestinationStripeId}) from charge {ChargeId}",
            r.Amount, r.PayeeId, destinationStripeId, r.ChargeId);

        return await transferClient.ReleaseAsync(new StripeReleaseOptions
        {
            Amount = r.Amount,
            ChargeId = r.ChargeId,
            DestinationStripeId = destinationStripeId,
            Metadata = metadata
        });
    }

    public async Task<Result<RefundResponse>> RefundAsync(RefundRequest r, CancellationToken ct = default)
    {
        var metadata = new Dictionary<string, string>
        {
            ["amount"] = ((long)(r.Amount * 100)).ToString()
        }
        .Merge(r.Metadata);

        logger.LogInformation(
            "Refunding {Amount} GBP for payment intent {IntentId}{TransferReversal}",
            r.Amount, r.PaymentIntentId, string.IsNullOrEmpty(r.TransferId) ? string.Empty : $" (reversing transfer {r.TransferId})");

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
            logger.LogInformation(
                "Capturing PaymentIntent {PaymentIntentId} for {Purpose}",
                r.PaymentIntentId, r.Metadata["type"]);

            await stripeHoldClient.CaptureAsync(r.PaymentIntentId, r.Metadata, ct);
            return Result.Ok();
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe capture failed for PaymentIntent {PaymentIntentId}: {StripeCode}",
                r.PaymentIntentId, ex.StripeError?.Code);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Capture failed for PaymentIntent {PaymentIntentId}",
                r.PaymentIntentId);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }
}
