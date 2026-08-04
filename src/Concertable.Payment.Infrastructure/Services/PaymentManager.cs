using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
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

    public Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, amount, transferAmount: null, paymentMethodId, session, metadata, ct);

    public Task<Result<PaymentOutcome, PaymentError>> SettleAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, chargeAmount, payeeAmount, paymentMethodId, session, metadata, ct);

    private async Task<Result<PaymentOutcome, PaymentError>> ChargeInternalAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money? transferAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        var accountResult = await ResolveChargeAccountsAsync(payerId, payeeId, ct);
        if (!accountResult.TryGetValue(out var accounts))
            return accountResult.Match(
                _ => throw new InvalidOperationException("A successful Result must contain a value."),
                Result.Failure<PaymentOutcome, PaymentError>);
        var (stripeCustomerId, destinationStripeId, receiptEmail) = accounts;

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

    public async Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var accountResult = await ResolveChargeAccountsAsync(payerId, payeeId, ct);
        if (!accountResult.TryGetValue(out var accounts))
            return accountResult.Match(
                _ => throw new InvalidOperationException("A successful Result must contain a value."),
                Result.Failure<PaymentOutcome, PaymentError>);
        var (stripeCustomerId, destinationStripeId, receiptEmail) = accounts;

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

    public async Task<Result<Transfer, ReleaseError>> ReleaseAsync(ReleaseRequest r, CancellationToken ct = default)
    {
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(r.PayeeId, ct);
        if (payeeAccount is null)
            return Result.Failure<Transfer, ReleaseError>(ReleaseError.RecipientUnavailable);

        var destinationStripeId = payeeAccount.StripeAccountId;
        if (destinationStripeId is null)
            return Result.Failure<Transfer, ReleaseError>(ReleaseError.RecipientUnavailable);

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

    public async Task<Result<Refund, RefundError>> RefundAsync(RefundRequest r, CancellationToken ct = default)
    {
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Amount] = r.Amount.ToMinorUnits().ToString()
        }
        .Merge(r.Metadata);

        logger.RefundingPayment(
            r.Amount.Amount,
            r.PaymentIntentId,
            r.TransferReversal is null ? string.Empty : $" (reversing transfer {r.TransferReversal.TransferId})");

        return await transferClient.RefundAsync(new StripeRefundOptions
        {
            Amount = r.Amount,
            PaymentIntentId = r.PaymentIntentId,
            TransferReversal = r.TransferReversal,
            ReverseTransfer = r.ReverseTransfer,
            Reason = r.Reason,
            Metadata = metadata
        });
    }

    public async Task<UnitResult<CaptureError>> CaptureAsync(CaptureRequest r, CancellationToken ct = default)
    {
        try
        {
            logger.CapturingPaymentIntent(r.PaymentIntentId, r.Metadata[PaymentMetadataKeys.Type]);

            await stripeHoldClient.CaptureAsync(r.PaymentIntentId, r.Metadata, ct);
            return UnitResult.Success<CaptureError>();
        }
        catch (StripeException ex)
        {
            logger.StripeCaptureFailedForPaymentIntent(r.PaymentIntentId, ex.StripeError?.Code, ex);
            if (ex.StripeError?.Type is "card_error" or "invalid_request_error")
                return UnitResult.Failure<CaptureError>(new CaptureError.PaymentFailure(new PaymentError.PaymentRejected()));
            throw;
        }
    }

    private async Task<Result<(string stripeCustomerId, string destinationStripeId, string email), PaymentError>> ResolveChargeAccountsAsync(
        Guid payerId,
        Guid payeeId,
        CancellationToken ct)
    {
        var payerAccount = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payerAccount is null)
            return Result.Failure<(string, string, string), PaymentError>(new PaymentError.PayerNotFound());
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(payeeId, ct);
        if (payeeAccount is null)
            return Result.Failure<(string, string, string), PaymentError>(new PaymentError.PayeeNotFound());

        var stripeCustomerId = payerAccount.StripeCustomerId;
        if (stripeCustomerId is null)
            return Result.Failure<(string, string, string), PaymentError>(new PaymentError.PayerUnavailable());
        var destinationStripeId = payeeAccount.StripeAccountId;
        if (destinationStripeId is null)
            return Result.Failure<(string, string, string), PaymentError>(new PaymentError.RecipientUnavailable());

        return Result.Success<(string, string, string), PaymentError>((stripeCustomerId, destinationStripeId, payerAccount.Email));
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
