using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Kernel.Exceptions;
using FluentResults;
using Microsoft.Extensions.Logging;
using Stripe;
using Transfer = Concertable.Payment.Contracts.Transfer;
using Refund = Concertable.Payment.Contracts.Refund;

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

    public Task<Result<PaymentOutcome>> ChargeAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, amount, transferAmount: null, paymentMethodId, session, metadata, ct);

    public Task<Result<PaymentOutcome>> SettleAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, chargeAmount, payeeAmount, paymentMethodId, session, metadata, ct);

    private async Task<Result<PaymentOutcome>> ChargeInternalAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money? transferAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        var (stripeCustomerId, destinationStripeId, receiptEmail) = await ResolveChargeAccountsAsync(payerId, payeeId, ct);

        var payeeAmount = transferAmount ?? chargeAmount;

        var merged = BuildMetadata(payerId, payeeId, receiptEmail, payeeAmount, metadata);

        logger.ChargingPayment(payerId, payeeAmount.Amount, payeeId, destinationStripeId, metadata[PaymentMetadataKeys.Type]);

        return await intentClientFactory.Create(session).ChargeAsync(new StripeChargeOptions
        {
            Amount = chargeAmount,
            TransferAmount = transferAmount,
            PaymentMethodId = paymentMethodId,
            StripeCustomerId = stripeCustomerId,
            DestinationStripeId = destinationStripeId,
            ReceiptEmail = receiptEmail,
            Metadata = merged
        });
    }

    public async Task<Result<PaymentOutcome>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var (stripeCustomerId, destinationStripeId, receiptEmail) = await ResolveChargeAccountsAsync(payerId, payeeId, ct);

        var merged = BuildMetadata(payerId, payeeId, receiptEmail, amount, metadata);

        logger.HoldingPayment(amount.Amount, payerId, payeeId, destinationStripeId, metadata[PaymentMetadataKeys.Type]);

        return await intentClientFactory.Create(session).HoldAsync(new StripeHoldOptions
        {
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            StripeCustomerId = stripeCustomerId,
            DestinationStripeId = destinationStripeId,
            ReceiptEmail = receiptEmail,
            Metadata = merged
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
            [PaymentMetadataKeys.ToUserId] = r.PayeeId.ToString(),
            [PaymentMetadataKeys.Amount] = r.Amount.ToMinorUnits().ToString()
        }
        .Merge(r.Metadata);

        logger.ReleasingPayment(r.Amount.Amount, r.PayeeId, destinationStripeId, r.ChargeId);

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
            [PaymentMetadataKeys.Amount] = r.Amount.ToMinorUnits().ToString()
        }
        .Merge(r.Metadata);

        logger.RefundingPayment(r.Amount.Amount, r.PaymentIntentId, string.IsNullOrEmpty(r.TransferId) ? string.Empty : $" (reversing transfer {r.TransferId})");

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
            logger.CapturingPaymentIntent(r.PaymentIntentId, r.Metadata[PaymentMetadataKeys.Type]);

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

    private async Task<(string stripeCustomerId, string destinationStripeId, string email)> ResolveChargeAccountsAsync(
        Guid payerId,
        Guid payeeId,
        CancellationToken ct)
    {
        var payerAccount = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(payeeId, ct)
            ?? throw new NotFoundException($"Payout account not found for payee {payeeId}");

        var stripeCustomerId = payerAccount.StripeCustomerId
            ?? throw new BadRequestException("Payer has no Stripe customer ID");
        var destinationStripeId = payeeAccount.StripeAccountId
            ?? throw new BadRequestException("Payee has no Stripe Connect account");

        return (stripeCustomerId, destinationStripeId, payerAccount.Email);
    }

    private static Dictionary<string, string> BuildMetadata(
        Guid payerId,
        Guid payeeId,
        string payerEmail,
        Money settledAmount,
        IReadOnlyDictionary<string, string> metadata) =>
        new Dictionary<string, string>
        {
            [PaymentMetadataKeys.FromUserId] = payerId.ToString(),
            [PaymentMetadataKeys.FromUserEmail] = payerEmail,
            [PaymentMetadataKeys.ToUserId] = payeeId.ToString(),
            [PaymentMetadataKeys.Amount] = settledAmount.ToMinorUnits().ToString()
        }
        .Merge(metadata);
}
