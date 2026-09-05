using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure;

internal sealed class SettlementService : ISettlementService
{
    private readonly IPaymentManager paymentManager;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly ITransactionRepository transactionRepository;
    private readonly ICommissionService commissionService;
    private readonly CommissionCalculator commissionCalculator;
    private readonly ILedgerService ledger;
    private readonly IUnitOfWork unitOfWork;
    private readonly IPaymentOperationResolver paymentOperationResolver;
    private readonly TimeProvider timeProvider;
    private readonly Money platformFee;

    public SettlementService(
        IPaymentManager paymentManager,
        IPayoutAccountRepository payoutAccountRepository,
        ITransactionRepository transactionRepository,
        ICommissionService commissionService,
        CommissionCalculator commissionCalculator,
        ILedgerService ledger,
        IUnitOfWork unitOfWork,
        IPaymentOperationResolver paymentOperationResolver,
        TimeProvider timeProvider,
        IOptions<PlatformFeeOptions> platformFeeOptions)
    {
        this.paymentManager = paymentManager;
        this.payoutAccountRepository = payoutAccountRepository;
        this.transactionRepository = transactionRepository;
        this.commissionService = commissionService;
        this.commissionCalculator = commissionCalculator;
        this.ledger = ledger;
        this.unitOfWork = unitOfWork;
        this.paymentOperationResolver = paymentOperationResolver;
        this.timeProvider = timeProvider;
        this.platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
    }

    public async Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        CancellationToken ct = default)
    {
        var resolved = await paymentOperationResolver.ResolvePaymentMethodAsync(paymentMethod, payerId, ct);
        if (!resolved.TryGetValue(out var paymentMethodId))
        {
            resolved.TryGetError(out var error);
            return new PaymentMethodChargeError.PaymentMethodFailure(error!);
        }

        var fingerprint = SettlementOperationFingerprint.CreateCharge(
            operationId,
            payerId,
            payeeId,
            amount,
            platformFee,
            paymentMethodId,
            session,
            reference);
        var existing = await transactionRepository.GetSettlementByOperationIdAsync(operationId, ct);
        if (existing is not null)
            return await ReplayAsync(existing, operationId, fingerprint, session, ct);

        var payerError = await ValidatePayerAsync(payerId, session, ct);
        if (payerError is not null)
            return new PaymentMethodChargeError.PaymentFailure(payerError);

        var metadata = ReferenceMetadata(reference);
        metadata[PaymentMetadataKeys.OperationId] = operationId.ToString();
        var charge = await paymentManager.SettleAsync(
            operationId,
            payerId,
            payeeId,
            amount + platformFee,
            amount,
            paymentMethodId,
            session,
            metadata,
            ct);
        if (!charge.TryGetValue(out var outcome))
        {
            charge.TryGetError(out var error);
            return error!.ToPaymentMethodChargeError();
        }

        var transaction = SettlementTransactionEntity.CreateForOperation(
            payerId,
            payeeId,
            outcome.ProviderTransactionId,
            (amount + platformFee).ToMinorUnits(),
            platformFee.ToMinorUnits(),
            TransactionStatus.Pending,
            reference,
            operationId,
            fingerprint,
            outcome.RequiresAction);
        await transactionRepository.AddAsync(transaction, ct);

        if (!outcome.RequiresAction && transaction.Complete(timeProvider.GetUtcNow().UtcDateTime).IsSuccess)
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            var canonical = await transactionRepository.ReloadSettlementByOperationIdAsync(operationId, ct);
            if (canonical is null)
                throw;

            return await ReplayAsync(canonical, operationId, fingerprint, session, ct);
        }

        return ToPublicOutcome(outcome);
    }

    public async Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default)
    {
        var resolved = await paymentOperationResolver.ResolvePaymentMethodAsync(paymentMethod, payerId, ct);
        if (!resolved.TryGetValue(out var paymentMethodId))
        {
            resolved.TryGetError(out var error);
            return new PaymentMethodChargeError.PaymentMethodFailure(error!);
        }

        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            gross,
            null,
            null,
            ct);
        if (!authorized.TryGetValue(out var bound))
        {
            authorized.TryGetError(out var error);
            return new PaymentMethodChargeError.CommissionFailure(error!);
        }

        var existing = await transactionRepository.GetSettlementByCommissionBindingIdAsync(commissionBindingId, ct);
        if (existing is not null)
        {
            return new PaymentOutcome { RequiresAction = existing.Status == TransactionStatus.Pending };
        }

        var payerError = await ValidatePayerAsync(payerId, session, ct);
        if (payerError is not null)
            return new PaymentMethodChargeError.PaymentFailure(payerError);

        var calculation = bound.Calculation;
        var charge = await paymentManager.SettleBoundCommissionAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            Money.FromMinorUnits(calculation.PayeeGrossMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(bound, reference),
            commissionBindingId,
            ct);
        if (!charge.TryGetValue(out var outcome))
        {
            charge.TryGetError(out var error);
            return error!.ToPaymentMethodChargeError();
        }

        commissionService.BindPaymentIntent(bound.Binding, outcome.ProviderTransactionId);
        var transaction = SettlementTransactionEntity.CreateBound(
            payerId,
            payeeId,
            outcome.ProviderTransactionId,
            calculation,
            TransactionStatus.Pending,
            reference,
            commissionBindingId);
        await transactionRepository.AddAsync(transaction, ct);

        if (!outcome.RequiresAction && transaction.Complete(timeProvider.GetUtcNow().UtcDateTime).IsSuccess)
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return ToPublicOutcome(outcome);
    }

    public async Task<Result<Option<Refund>, SettlementRefundError>> RefundBoundCommissionAsync(
        PaymentOperationReference reference,
        Money gross,
        string? reason = null,
        CancellationToken ct = default)
    {
        var grossMinor = gross.ToMinorUnits();
        var settlement = await transactionRepository.GetSettlementWithRefundsByReferenceAsync(reference, ct);
        if (settlement is null)
            return Result<Option<Refund>, SettlementRefundError>.Success(Option.None<Refund>());
        if (settlement.CommissionBindingId is null)
            return new SettlementRefundError.CommissionBindingNotFound();
        if (gross.Currency != settlement.Currency)
            return new SettlementRefundError.CurrencyMismatch();
        if (settlement.Status != TransactionStatus.Complete)
            return new SettlementRefundError.SettlementNotRefundable();
        if (grossMinor <= 0)
            return new SettlementRefundError.AmountMustBePositive();

        var countedRefunds = settlement.Refunds.Where(refund => refund.CountsTowardCumulative).ToList();
        var grossAlreadyRefunded = countedRefunds.Sum(refund => refund.GrossRefundedMinor);
        var cumulativeGrossRefund = checked(grossAlreadyRefunded + grossMinor);
        if (cumulativeGrossRefund > settlement.PayeeGrossMinor)
            return new SettlementRefundError.AmountExceedsRemaining();

        var cumulativeCommissionRefund = commissionCalculator.CalculateCumulativeRefund(
            settlement.CommissionGrossMinor,
            cumulativeGrossRefund,
            settlement.PayeeGrossMinor);
        var cumulativeVatReversal = commissionCalculator.CalculateCumulativeRefund(
            settlement.CommissionVatMinor,
            cumulativeGrossRefund,
            settlement.PayeeGrossMinor);
        var commissionRefundMinor = checked(
            cumulativeCommissionRefund - countedRefunds.Sum(refund => refund.CommissionRefundedMinor));
        var commissionVatReversedMinor = checked(
            cumulativeVatReversal - countedRefunds.Sum(refund => refund.CommissionVatReversedMinor));
        var payerTotalRefundMinor = checked(grossMinor + commissionRefundMinor);

        if (!await transactionRepository.TryReserveSettlementRefundGrossAsync(settlement.Id, grossMinor, ct))
            return await ReservationConflictAsync(reference, grossMinor, ct);

        var reservation = PaymentRefundEntity.CreatePendingForSettlement(
            settlement.Id,
            grossMinor,
            commissionRefundMinor,
            commissionVatReversedMinor,
            timeProvider.GetUtcNow());
        if (settlement.RecordRefund(reservation).IsFailure)
        {
            await transactionRepository.ReleaseReservedSettlementRefundGrossAsync(settlement.Id, grossMinor, ct);
            throw new InvalidOperationException("Settlement refund reservation could not be recorded.");
        }

        await unitOfWork.SaveChangesAsync(ct);

        var metadata = ReferenceMetadata(reference);
        metadata[PaymentMetadataKeys.Type] = TransactionTypes.SettlementRefund;
        metadata[PaymentMetadataKeys.CommissionBindingId] = settlement.CommissionBindingId.Value.ToString();
        metadata[PaymentMetadataKeys.PayeeGrossMinor] = grossMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionGrossMinor] = commissionRefundMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionVatMinor] = commissionVatReversedMinor.ToString();
        metadata[PaymentMetadataKeys.PayerTotalMinor] = payerTotalRefundMinor.ToString();
        metadata[PaymentMetadataKeys.CumulativeGrossRefundMinor] = cumulativeGrossRefund.ToString();

        var refund = await paymentManager.RefundAsync(new RefundRequest
        {
            Amount = payerTotalRefundMinor.ToMoney(settlement.Currency),
            PaymentIntentId = settlement.PaymentIntentId,
            ReverseTransfer = true,
            Reason = reason,
            CommissionBindingId = settlement.CommissionBindingId,
            RefundId = reservation.Id,
            Metadata = metadata
        }, ct);
        if (!refund.TryGetValue(out var completedRefund))
        {
            if (settlement.ReleaseRefund(reservation).IsFailure)
                throw new InvalidOperationException("Settlement refund reservation could not be released.");
            await unitOfWork.SaveChangesAsync(ct);
            await transactionRepository.ReleaseReservedSettlementRefundGrossAsync(settlement.Id, grossMinor, ct);
            refund.TryGetError(out var error);
            return new SettlementRefundError.PaymentFailure(error!);
        }

        if (settlement.CompleteRefund(reservation, completedRefund.ProviderRefundId, timeProvider.GetUtcNow()).IsFailure)
            throw new InvalidOperationException("Settlement refund reservation could not be completed.");

        await ledger.StageAsync(
            LedgerPostings.DirectSettlementRefund(
                settlement.PayerId,
                settlement.PayeeId,
                grossMinor.ToMoney(settlement.Currency),
                checked(commissionRefundMinor - commissionVatReversedMinor).ToMoney(settlement.Currency),
                commissionVatReversedMinor.ToMoney(settlement.Currency),
                reference,
                settlement.PaymentIntentId,
                completedRefund.ProviderRefundId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Option.Some(new Refund(reservation.Id));
    }

    private async Task<Result<Option<Refund>, SettlementRefundError>> ReservationConflictAsync(
        PaymentOperationReference reference,
        long grossMinor,
        CancellationToken ct)
    {
        var current = await transactionRepository.GetSettlementWithRefundsByReferenceAsync(reference, ct);
        if (current is null)
            return new SettlementRefundError.SettlementNotFound();
        if (current.Status != TransactionStatus.Complete)
            return new SettlementRefundError.SettlementNotRefundable();
        return checked(current.RefundedGrossMinor + grossMinor) > current.PayeeGrossMinor
            ? new SettlementRefundError.AmountExceedsRemaining()
            : new SettlementRefundError.Conflict();
    }

    private async Task<Result<PaymentOutcome, PaymentMethodChargeError>> ReplayAsync(
        SettlementTransactionEntity transaction,
        Guid operationId,
        SettlementOperationFingerprint fingerprint,
        PaymentSession session,
        CancellationToken ct)
    {
        if (!transaction.MatchesOperation(operationId, fingerprint))
            return new PaymentMethodChargeError.OperationConflict();
        if (transaction.Status == TransactionStatus.Complete || !transaction.RequiresAction)
        {
            return new PaymentOutcome();
        }

        var result = await paymentManager.GetPaymentOutcomeAsync(transaction.PaymentIntentId, session, ct);
        if (result.TryGetValue(out var outcome))
            return ToPublicOutcome(outcome);

        result.TryGetError(out var error);
        return new PaymentMethodChargeError.PaymentFailure(error!);
    }

    private async Task<PaymentError?> ValidatePayerAsync(
        Guid payerId,
        PaymentSession session,
        CancellationToken ct)
    {
        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return new PaymentError.PayerNotFound();
        return session == PaymentSession.OffSession && payer.StripeCustomerId is null
            ? new PaymentError.PayerUnavailable()
            : null;
    }

    private static PaymentOutcome ToPublicOutcome(ProviderPaymentOutcome outcome) => new()
    {
        RequiresAction = outcome.RequiresAction,
        ClientSecret = outcome.ClientSecret
    };

    private static Dictionary<string, string> ReferenceMetadata(PaymentOperationReference reference) =>
        new()
        {
            [PaymentMetadataKeys.Type] = TransactionTypes.Settlement,
            [PaymentMetadataKeys.OperationType] = reference.OperationType,
            [PaymentMetadataKeys.ClientReference] = reference.ClientReference
        };

    private static IReadOnlyDictionary<string, string> CommissionMetadata(
        BoundCommission authorized,
        PaymentOperationReference reference)
    {
        var calculation = authorized.Calculation;
        var metadata = ReferenceMetadata(reference);
        metadata[PaymentMetadataKeys.CommissionBindingId] = authorized.Binding.Id.ToString();
        metadata[PaymentMetadataKeys.Currency] = calculation.Currency.ToString().ToLowerInvariant();
        metadata[PaymentMetadataKeys.PayeeGrossMinor] = calculation.PayeeGrossMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionGrossMinor] = calculation.CommissionGrossMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionNetMinor] = calculation.CommissionNetMinor.ToString();
        metadata[PaymentMetadataKeys.CommissionVatMinor] = calculation.CommissionVatMinor.ToString();
        metadata[PaymentMetadataKeys.PayerTotalMinor] = calculation.PayerTotalMinor.ToString();
        return metadata;
    }
}
