using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
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

    public async Task<Result<ProviderPaymentOutcome, PaymentError>> ChargeAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        (await ChargeInternalAsync(payerId, payeeId, amount, null, paymentMethodId, session, metadata, null, null, ct))
            .MapError(rejection => rejection.ToPaymentError());

    public Task<Result<ProviderPaymentOutcome, ChargeError>> SettleAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, chargeAmount, payeeAmount, paymentMethodId, session, metadata, null, null, ct);

    public Task<Result<ProviderPaymentOutcome, ChargeError>> SettleAsync(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, chargeAmount, payeeAmount, paymentMethodId, session, metadata, operationId, null, ct);

    public Task<Result<ProviderPaymentOutcome, ChargeError>> SettleBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money payeeAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        CancellationToken ct = default) =>
        ChargeInternalAsync(payerId, payeeId, chargeAmount, payeeAmount, paymentMethodId, session, metadata, null, commissionBindingId, ct);

    public Task<Result<ProviderPaymentOutcome, PaymentError>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        HoldInternalAsync(payerId, payeeId, amount, paymentMethodId, session, metadata, null, null, ct);

    public Task<Result<ProviderPaymentOutcome, PaymentError>> HoldAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid operationId,
        CancellationToken ct = default) =>
        HoldInternalAsync(payerId, payeeId, amount, paymentMethodId, session, metadata, operationId, null, ct);

    public Task<Result<ProviderPaymentOutcome, PaymentError>> HoldBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        CancellationToken ct = default) =>
        HoldInternalAsync(payerId, payeeId, amount, paymentMethodId, session, metadata, null, commissionBindingId, ct);

    public Task<Result<ProviderPaymentOutcome, PaymentError>> GetPaymentOutcomeAsync(
        string paymentIntentId,
        PaymentSession session,
        CancellationToken ct = default) =>
        intentClientFactory.Create(session).GetAsync(paymentIntentId, ct);

    private async Task<Result<ProviderPaymentOutcome, PaymentError>> HoldInternalAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        Guid? commissionBindingId,
        CancellationToken ct)
    {
        var accounts = await ResolveChargeAccountsAsync(payerId, payeeId, ct);
        if (!accounts.TryGetValue(out var resolved))
        {
            accounts.TryGetError(out var error);
            return Result<ProviderPaymentOutcome, PaymentError>.Failure(error!);
        }

        var merged = BuildMetadata(payerId, payeeId, resolved.email, amount, metadata);
        logger.HoldingPayment(amount.Amount, payerId, payeeId, resolved.destinationStripeId, metadata[PaymentMetadataKeys.Type]);

        return await intentClientFactory.Create(session).HoldAsync(new StripeHoldOptions
        {
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            StripeCustomerId = resolved.stripeCustomerId,
            DestinationStripeId = resolved.destinationStripeId,
            ReceiptEmail = resolved.email,
            OperationId = operationId,
            CommissionBindingId = commissionBindingId,
            Metadata = merged
        }, ct);
    }

    public async Task<Result<ProviderTransfer, PaymentError>> ReleaseAsync(ReleaseRequest request, CancellationToken ct = default)
    {
        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(request.PayeeId, ct);
        if (payeeAccount is null)
            return Result<ProviderTransfer, PaymentError>.Failure(new PaymentError.PayeeNotFound());
        if (payeeAccount.StripeAccountId is null)
            return Result<ProviderTransfer, PaymentError>.Failure(new PaymentError.RecipientUnavailable());

        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.PayeeOwnerId] = request.PayeeId.ToString(),
            [PaymentMetadataKeys.AmountMinor] = request.Amount.ToMinorUnits().ToString()
        }.Merge(request.Metadata);

        logger.ReleasingPayment(request.Amount.Amount, request.PayeeId, payeeAccount.StripeAccountId, request.ChargeId);

        return await transferClient.ReleaseAsync(new StripeReleaseOptions
        {
            Amount = request.Amount,
            ChargeId = request.ChargeId,
            DestinationStripeId = payeeAccount.StripeAccountId,
            OperationId = request.OperationId,
            CommissionBindingId = request.CommissionBindingId,
            Metadata = metadata
        }, ct);
    }

    public async Task<Result<ProviderRefund, PaymentError>> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.AmountMinor] = request.Amount.ToMinorUnits().ToString()
        }.Merge(request.Metadata);

        logger.RefundingPayment(
            request.Amount.Amount,
            request.PaymentIntentId,
            request.TransferReversal is null ? string.Empty : $" (reversing transfer {request.TransferReversal.TransferId})");

        return await transferClient.RefundAsync(new StripeRefundOptions
        {
            Amount = request.Amount,
            PaymentIntentId = request.PaymentIntentId,
            TransferReversal = request.TransferReversal,
            ReverseTransfer = request.ReverseTransfer,
            Reason = request.Reason,
            OperationId = request.OperationId,
            CommissionBindingId = request.CommissionBindingId,
            RefundId = request.RefundId,
            Metadata = metadata
        }, ct);
    }

    public async Task<UnitResult<PaymentError>> CaptureAsync(CaptureRequest request, CancellationToken ct = default)
    {
        try
        {
            logger.CapturingPaymentIntent(request.PaymentIntentId, request.Metadata[PaymentMetadataKeys.Type]);
            await stripeHoldClient.CaptureAsync(
                request.PaymentIntentId,
                request.Metadata,
                request.OperationId,
                request.CommissionBindingId,
                ct);
            return UnitResult.Success<PaymentError>();
        }
        catch (StripeException ex)
        {
            logger.StripeCaptureFailedForPaymentIntent(request.PaymentIntentId, ex.StripeError?.Code, ex);
            if (StripeFailureClassifier.Classify(ex).TryGetValue(out var rejection))
                return UnitResult.Failure(rejection.ToPaymentError());
            throw;
        }
    }

    private async Task<Result<ProviderPaymentOutcome, ChargeError>> ChargeInternalAsync(
        Guid payerId,
        Guid payeeId,
        Money chargeAmount,
        Money? transferAmount,
        string paymentMethodId,
        PaymentSession session,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        Guid? commissionBindingId,
        CancellationToken ct)
    {
        var accounts = await ResolveChargeAccountsAsync(payerId, payeeId, ct);
        if (!accounts.TryGetValue(out var resolved))
        {
            accounts.TryGetError(out var error);
            return Result<ProviderPaymentOutcome, ChargeError>.Failure(new ChargeError.PaymentFailure(error!));
        }

        var payeeAmount = transferAmount ?? chargeAmount;
        var merged = BuildMetadata(payerId, payeeId, resolved.email, payeeAmount, metadata);
        logger.ChargingPayment(payerId, payeeAmount.Amount, payeeId, resolved.destinationStripeId, metadata[PaymentMetadataKeys.Type]);

        return await intentClientFactory.Create(session).ChargeAsync(new StripeChargeOptions
        {
            Amount = chargeAmount,
            TransferAmount = transferAmount,
            PaymentMethodId = paymentMethodId,
            StripeCustomerId = resolved.stripeCustomerId,
            DestinationStripeId = resolved.destinationStripeId,
            ReceiptEmail = resolved.email,
            OperationId = operationId,
            CommissionBindingId = commissionBindingId,
            Metadata = merged
        }, ct);
    }

    private async Task<Result<(string stripeCustomerId, string destinationStripeId, string email), PaymentError>> ResolveChargeAccountsAsync(
        Guid payerId,
        Guid payeeId,
        CancellationToken ct)
    {
        var payerAccount = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payerAccount is null)
            return Result<(string, string, string), PaymentError>.Failure(new PaymentError.PayerNotFound());

        var payeeAccount = await payoutAccountRepository.GetByOwnerIdAsync(payeeId, ct);
        if (payeeAccount is null)
            return Result<(string, string, string), PaymentError>.Failure(new PaymentError.PayeeNotFound());
        if (payerAccount.StripeCustomerId is null)
            return Result<(string, string, string), PaymentError>.Failure(new PaymentError.PayerUnavailable());
        if (payeeAccount.StripeAccountId is null)
            return Result<(string, string, string), PaymentError>.Failure(new PaymentError.RecipientUnavailable());

        return Result<(string, string, string), PaymentError>.Success(
            (payerAccount.StripeCustomerId, payeeAccount.StripeAccountId, payerAccount.Email));
    }

    private static Dictionary<string, string> BuildMetadata(
        Guid payerId,
        Guid payeeId,
        string payerEmail,
        Money settledAmount,
        IReadOnlyDictionary<string, string> metadata) =>
        new Dictionary<string, string>
        {
            [PaymentMetadataKeys.PayerOwnerId] = payerId.ToString(),
            [PaymentMetadataKeys.PayerEmail] = payerEmail,
            [PaymentMetadataKeys.PayeeOwnerId] = payeeId.ToString(),
            [PaymentMetadataKeys.AmountMinor] = settledAmount.ToMinorUnits().ToString()
        }.Merge(metadata);
}
