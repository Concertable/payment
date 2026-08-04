using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Exceptions;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class ManagerPaymentService : IManagerPaymentService
{
    private readonly IPaymentManager paymentManager;
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly IStripeHoldClient stripeHoldClient;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ICommissionService commissionService;
    private readonly CommissionCalculator commissionCalculator;
    private readonly ILedgerService ledger;
    private readonly IUnitOfWork unitOfWork;
    private readonly TimeProvider timeProvider;
    private readonly Money platformFee;

    public ManagerPaymentService(
        IPaymentManager paymentManager,
        IStripeAccountClient stripeAccountClient,
        IStripeHoldClient stripeHoldClient,
        IPayoutAccountRepository payoutAccountRepository,
        ITransactionRepository transactionRepository,
        ICommissionService commissionService,
        CommissionCalculator commissionCalculator,
        ILedgerService ledger,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<PlatformFeeOptions> platformFeeOptions)
    {
        this.paymentManager = paymentManager;
        this.stripeAccountClient = stripeAccountClient;
        this.stripeHoldClient = stripeHoldClient;
        this.payoutAccountRepository = payoutAccountRepository;
        this.transactionRepository = transactionRepository;
        this.commissionService = commissionService;
        this.commissionCalculator = commissionCalculator;
        this.ledger = ledger;
        this.unitOfWork = unitOfWork;
        this.timeProvider = timeProvider;
        this.platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
    }

    public async Task<Result<PaymentOutcome, PaymentError>> PayAsync(
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
            return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PayerNotFound());

        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PayerUnavailable());

        var charge = await paymentManager.SettleAsync(
            payerId,
            payeeId,
            amount + platformFee,
            amount,
            paymentMethodId,
            session,
            new Dictionary<string, string>
            {
                [PaymentMetadataKeys.Type] = TransactionTypes.Settlement,
                [PaymentMetadataKeys.BookingId] = bookingId.ToString()
            },
            ct);

        if (!charge.TryGetValue(out var outcome))
            return charge;

        if (string.IsNullOrEmpty(outcome.TransactionId))
            throw new InvalidOperationException("Stripe charge response missing PaymentIntent id.");

        var transaction = SettlementTransactionEntity.Create(
            payerId,
            payeeId,
            outcome.TransactionId,
            (amount + platformFee).ToMinorUnits(),
            platformFee.ToMinorUnits(),
            TransactionStatus.Pending,
            bookingId);

        await transactionRepository.CreateAsync(transaction);

        if (!outcome.RequiresAction && transaction.Complete())
        {
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return charge;
    }

    public async Task<Result<PaymentOutcome, PaymentError>> PayBoundCommissionAsync(
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
        var existing = await transactionRepository.GetSettlementByCommissionBindingIdAsync(
            commissionBindingId,
            ct);
        if (existing is not null)
            return Result.Success<PaymentOutcome, PaymentError>(new PaymentOutcome
            {
                TransactionId = existing.PaymentIntentId,
                RequiresAction = existing.Status == TransactionStatus.Pending
            });

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
            return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.CommissionFailure(commissionError));
        }

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PayerNotFound());
        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PayerUnavailable());

        var calculation = boundCommission.Calculation;
        var charge = await paymentManager.SettleAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            Money.FromMinorUnits(calculation.PayeeGrossMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(boundCommission, bookingId),
            ct);
        if (!charge.TryGetValue(out var outcome))
            return charge;
        if (string.IsNullOrEmpty(outcome.TransactionId))
            throw new InvalidOperationException("Stripe charge response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(
            boundCommission.Binding,
            outcome.TransactionId);
        var transaction = SettlementTransactionEntity.CreateBound(
            payerId,
            payeeId,
            outcome.TransactionId,
            calculation,
            TransactionStatus.Pending,
            bookingId,
            commissionBindingId);
        await transactionRepository.AddAsync(transaction, ct);

        if (!outcome.RequiresAction && transaction.Complete())
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return charge;
    }

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        return await stripeAccountClient.CreateSetupSessionAsync(stripeCustomerId, metadata, ct);
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        return await stripeAccountClient.CreateVerifySessionAsync(stripeCustomerId, metadata, ct);
    }

    public async Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        return await stripeAccountClient.CreateHoldSessionAsync(stripeCustomerId, amount + platformFee, metadata, ct);
    }

    public async Task<Result<CheckoutSession, CommissionError>> CreateBoundCommissionHoldSessionAsync(
        Guid payerId,
        long grossMinor,
        Currency currency,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var boundIntentId = await commissionService.FindBoundPaymentIntentAsync(commissionBindingId, ct);

        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            currency,
            grossMinor,
            expectedCommissionMinor,
            expectedPayerTotalMinor,
            boundIntentId,
            stripeSetupIntentId,
            ct);
        if (!authorized.TryGetValue(out var boundCommission))
        {
            if (!authorized.TryGetError(out var error))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<CheckoutSession, CommissionError>(error);
        }

        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);

        if (!string.IsNullOrWhiteSpace(boundIntentId))
            return Result.Success<CheckoutSession, CommissionError>(await stripeAccountClient.GetHoldSessionAsync(stripeCustomerId, boundIntentId, ct));

        var calculation = boundCommission.Calculation;
        var session = await stripeAccountClient.CreateHoldSessionAsync(
            stripeCustomerId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            new Dictionary<string, string>(CommissionMetadata(boundCommission, bookingId: null))
                .Merge(metadata),
            ct);
        if (string.IsNullOrWhiteSpace(session.StripeIntentId))
            throw new InvalidOperationException("Stripe hold session response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(
            boundCommission.Binding,
            session.StripeIntentId);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success<CheckoutSession, CommissionError>(session);
    }

    public async Task<string> FindHeldIntentAsync(
        Guid payerId,
        int applicationId,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        var stripeCustomerId = account?.StripeCustomerId
            ?? throw new NotFoundException($"No Stripe customer for payer {payerId}");
        return await stripeHoldClient.FindHeldIntentAsync(stripeCustomerId, applicationId, ct);
    }

    public async Task<Result<Option<Refund>, RefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        string? reason = null,
        CancellationToken ct = default)
    {
        var settlement = await transactionRepository.GetSettlementWithRefundsByBookingIdAsync(bookingId, ct);
        if (settlement is null)
            return Result.Success<Option<Refund>, RefundError>(Option.None<Refund>());

        if (settlement.CommissionBindingId is null)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.CommissionBindingNotFound);
        if (currency != settlement.Currency)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.CurrencyMismatch);
        if (settlement.Status != TransactionStatus.Complete)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidEscrowState);
        if (grossMinor <= 0)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidAmount);

        var grossAlreadyRefunded = settlement.Refunds
            .Where(r => r.CountsTowardCumulative)
            .Sum(r => r.GrossRefundedMinor);
        var cumulativeGrossRefund = checked(grossAlreadyRefunded + grossMinor);
        if (cumulativeGrossRefund > settlement.PayeeGrossMinor)
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidAmount);

        var cumulativeCommissionRefund = commissionCalculator.CalculateCumulativeRefund(
            settlement.CommissionGrossMinor,
            cumulativeGrossRefund,
            settlement.PayeeGrossMinor);
        var cumulativeVatReversal = commissionCalculator.CalculateCumulativeRefund(
            settlement.CommissionVatMinor,
            cumulativeGrossRefund,
            settlement.PayeeGrossMinor);
        var commissionRefundMinor = checked(
            cumulativeCommissionRefund -
            settlement.Refunds.Where(r => r.CountsTowardCumulative).Sum(r => r.CommissionRefundedMinor));
        var commissionVatReversedMinor = checked(
            cumulativeVatReversal -
            settlement.Refunds.Where(r => r.CountsTowardCumulative).Sum(r => r.CommissionVatReversedMinor));
        var payerTotalRefundMinor = checked(grossMinor + commissionRefundMinor);

        if (!await transactionRepository.TryReserveSettlementRefundGrossAsync(settlement.Id, grossMinor, ct))
            return Result.Failure<Option<Refund>, RefundError>(RefundError.InvalidAmount);

        var reservation = PaymentRefundEntity.CreatePendingForSettlement(
            settlement.Id,
            grossMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            timeProvider.GetUtcNow());
        settlement.RecordRefund(reservation);
        await unitOfWork.SaveChangesAsync(ct);

        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Type] = TransactionTypes.SettlementRefund,
            [PaymentMetadataKeys.BookingId] = settlement.BookingId.ToString(),
            [PaymentMetadataKeys.CommissionBindingId] = settlement.CommissionBindingId.Value.ToString(),
            [PaymentMetadataKeys.PayeeGrossMinor] = grossMinor.ToString(),
            [PaymentMetadataKeys.CommissionGrossMinor] = commissionRefundMinor.ToString(),
            [PaymentMetadataKeys.CommissionVatMinor] = commissionVatReversedMinor.ToString(),
            [PaymentMetadataKeys.PayerTotalMinor] = payerTotalRefundMinor.ToString(),
            [PaymentMetadataKeys.CumulativeGrossRefundMinor] = cumulativeGrossRefund.ToString()
        };

        var refund = await paymentManager.RefundAsync(new RefundRequest
        {
            Amount = payerTotalRefundMinor.ToMoney(settlement.Currency),
            PaymentIntentId = settlement.PaymentIntentId,
            ReverseTransfer = true,
            Reason = reason,
            Metadata = metadata
        }, ct);
        if (!refund.TryGetValue(out var refundValue))
        {
            settlement.ReleaseRefund(reservation);
            await unitOfWork.SaveChangesAsync(ct);
            await transactionRepository.ReleaseReservedSettlementRefundGrossAsync(settlement.Id, grossMinor, ct);
            if (!refund.TryGetError(out var error))
                throw new InvalidOperationException("Failure result did not contain an error.");
            return Result.Failure<Option<Refund>, RefundError>(error);
        }

        settlement.CompleteRefund(reservation, refundValue.RefundId, timeProvider.GetUtcNow());

        await ledger.StageAsync(
            LedgerPostings.DirectSettlementRefund(
                settlement.PayerId,
                settlement.PayeeId,
                grossMinor.ToMoney(settlement.Currency),
                checked(commissionRefundMinor - commissionVatReversedMinor).ToMoney(settlement.Currency),
                commissionVatReversedMinor.ToMoney(settlement.Currency),
                settlement.BookingId,
                settlement.PaymentIntentId,
                refundValue.RefundId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success<Option<Refund>, RefundError>(Option.Some(refundValue));
    }

    private async Task<string> EnsureStripeCustomerAsync(Guid ownerId, CancellationToken ct)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? throw new NotFoundException($"Payout account not found for owner {ownerId}");

        if (account.StripeCustomerId is not null)
            return account.StripeCustomerId;

        await stripeAccountClient.ProvisionCustomerAsync(ownerId, account.Email, ct);

        var refreshed = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct);
        return refreshed?.StripeCustomerId
            ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
    }

    private static IReadOnlyDictionary<string, string> CommissionMetadata(
        BoundCommission authorized,
        int? bookingId)
    {
        var calculation = authorized.Calculation;
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Type] = TransactionTypes.Settlement,
            [PaymentMetadataKeys.CommissionBindingId] = authorized.Binding.Id.ToString(),
            [PaymentMetadataKeys.Currency] = calculation.Currency.ToString().ToLowerInvariant(),
            [PaymentMetadataKeys.PayeeGrossMinor] = calculation.PayeeGrossMinor.ToString(),
            [PaymentMetadataKeys.CommissionGrossMinor] = calculation.CommissionGrossMinor.ToString(),
            [PaymentMetadataKeys.CommissionNetMinor] = calculation.CommissionNetMinor.ToString(),
            [PaymentMetadataKeys.CommissionVatMinor] = calculation.CommissionVatMinor.ToString(),
            [PaymentMetadataKeys.PayerTotalMinor] = calculation.PayerTotalMinor.ToString()
        };
        if (bookingId is not null)
            metadata[PaymentMetadataKeys.BookingId] = bookingId.Value.ToString();

        return metadata;
    }
}
