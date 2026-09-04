using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Exceptions;
using Concertable.Kernel.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

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
    private readonly IPaymentOperationResolver paymentOperationResolver;
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
        IPaymentOperationResolver paymentOperationResolver,
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
        this.paymentOperationResolver = paymentOperationResolver;
        this.timeProvider = timeProvider;
        this.platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
    }

    public async Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayAsync(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var resolved = await paymentOperationResolver.ResolvePaymentMethodAsync(paymentMethod, payerId, ct);
        if (!resolved.TryGetValue(out var paymentMethodId))
        {
            resolved.TryGetError(out var error);
            return new PaymentMethodChargeError.PaymentMethodFailure(error!);
        }

        var charged = await PayCoreAsync(
            operationId,
            payerId,
            payeeId,
            amount,
            paymentMethodId,
            session,
            bookingId,
            ct);
        return charged.Match<Result<PaymentOutcome, PaymentMethodChargeError>>(
            outcome => outcome,
            rejection => rejection switch
            {
                ManagerChargeError.AuthenticationRequired =>
                    new PaymentMethodChargeError.AuthenticationRequired(),
                ManagerChargeError.OperationFailure(var error) =>
                    new PaymentMethodChargeError.ChargeFailure(error)
            });
    }

    public async Task<Result<PaymentOutcome, ManagerPaymentOperationError>> PayAsync(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default) =>
        (await PayCoreAsync(operationId, payerId, payeeId, amount, paymentMethodId, session, bookingId, ct))
            .MapError(rejection => rejection.ToOperationError());

    public async Task<Result<PaymentOutcome, ManagerPaymentError>> PayAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var result = await PayCoreAsync(null, payerId, payeeId, amount, paymentMethodId, session, bookingId, ct);
        if (result.TryGetValue(out var outcome))
            return Result<PaymentOutcome, ManagerPaymentError>.Success(outcome);

        result.TryGetError(out var rejection);
        return rejection!.ToOperationError() is ManagerPaymentOperationError.ManagerFailure(var managerError)
            ? Result<PaymentOutcome, ManagerPaymentError>.Failure(managerError)
            : throw new InvalidOperationException("A legacy manager payment cannot produce an operation conflict.");
    }

    private async Task<Result<PaymentOutcome, ManagerChargeError>> PayCoreAsync(
        Guid? operationId,
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct)
    {
        SettlementOperationFingerprint? fingerprint = operationId is { } id
            ? SettlementOperationFingerprint.CreateCharge(
                id,
                payerId,
                payeeId,
                amount,
                platformFee,
                paymentMethodId,
                session,
                bookingId)
            : null;

        if (operationId is { } replayOperationId)
        {
            var existing = await transactionRepository.GetSettlementByOperationIdAsync(replayOperationId, ct);
            if (existing is not null)
                return await ReplayAsync(existing, replayOperationId, fingerprint!.Value, session, ct);
        }

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return Result<PaymentOutcome, ManagerChargeError>.Failure(
                new ManagerChargeError.OperationFailure(
                    new ManagerPaymentOperationError.ManagerFailure(
                        new ManagerPaymentError.PaymentFailure(new PaymentError.PayerNotFound()))));
        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            return Result<PaymentOutcome, ManagerChargeError>.Failure(
                new ManagerChargeError.OperationFailure(
                    new ManagerPaymentOperationError.ManagerFailure(
                        new ManagerPaymentError.PaymentFailure(new PaymentError.PayerUnavailable()))));

        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Type] = TransactionTypes.Settlement,
            [PaymentMetadataKeys.BookingId] = bookingId.ToString()
        };
        if (operationId is { } metadataOperationId)
            metadata[PaymentMetadataKeys.OperationId] = metadataOperationId.ToString();

        var charge = operationId is { } chargeOperationId
            ? await paymentManager.SettleAsync(
                chargeOperationId,
                payerId,
                payeeId,
                amount + platformFee,
                amount,
                paymentMethodId,
                session,
                metadata,
                ct)
            : await paymentManager.SettleAsync(
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
            charge.TryGetError(out var rejection);
            return Result<PaymentOutcome, ManagerChargeError>.Failure(rejection!.ToManagerChargeError());
        }

        var transaction = operationId is { } transactionOperationId
            ? SettlementTransactionEntity.CreateForOperation(
                payerId,
                payeeId,
                outcome.TransactionId,
                (amount + platformFee).ToMinorUnits(),
                platformFee.ToMinorUnits(),
                TransactionStatus.Pending,
                bookingId,
                transactionOperationId,
                fingerprint!.Value,
                outcome.RequiresAction)
            : SettlementTransactionEntity.Create(
                payerId,
                payeeId,
                outcome.TransactionId,
                (amount + platformFee).ToMinorUnits(),
                platformFee.ToMinorUnits(),
                TransactionStatus.Pending,
                bookingId);
        await transactionRepository.AddAsync(transaction, ct);

        if (!outcome.RequiresAction && transaction.Complete(timeProvider.GetUtcNow().UtcDateTime).IsSuccess)
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (operationId is not null && ex.IsDuplicateKey())
        {
            var canonical = await transactionRepository.ReloadSettlementByOperationIdAsync(operationId.Value, ct);
            if (canonical is null)
                throw;

            return await ReplayAsync(canonical, operationId.Value, fingerprint!.Value, session, ct);
        }

        return outcome;
    }

    private async Task<Result<PaymentOutcome, ManagerChargeError>> ReplayAsync(
        SettlementTransactionEntity transaction,
        Guid operationId,
        SettlementOperationFingerprint fingerprint,
        PaymentSession session,
        CancellationToken ct)
    {
        if (!transaction.MatchesOperation(operationId, fingerprint))
            return new ManagerChargeError.OperationFailure(new ManagerPaymentOperationError.OperationConflict());
        if (transaction.Status == TransactionStatus.Complete || !transaction.RequiresAction)
        {
            return new PaymentOutcome
            {
                TransactionId = transaction.PaymentIntentId
            };
        }

        var result = await paymentManager.GetPaymentOutcomeAsync(transaction.PaymentIntentId, session, ct);
        if (result.TryGetValue(out var outcome))
            return outcome;

        result.TryGetError(out var error);
        return new ManagerChargeError.OperationFailure(
            new ManagerPaymentOperationError.ManagerFailure(
                new ManagerPaymentError.PaymentFailure(error!)));
    }

    public async Task<Result<PaymentOutcome, ManagerPaymentError>> PayBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            gross,
            null,
            stripeSetupIntentId,
            ct);
        if (!authorized.TryGetValue(out var bound))
        {
            authorized.TryGetError(out var error);
            return Result<PaymentOutcome, ManagerPaymentError>.Failure(new ManagerPaymentError.CommissionFailure(error!));
        }

        var existing = await transactionRepository.GetSettlementByCommissionBindingIdAsync(commissionBindingId, ct);
        if (existing is not null)
            return Result<PaymentOutcome, ManagerPaymentError>.Success(new PaymentOutcome
            {
                TransactionId = existing.PaymentIntentId,
                RequiresAction = existing.Status == TransactionStatus.Pending
            });

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        if (payer is null)
            return Result<PaymentOutcome, ManagerPaymentError>.Failure(
                new ManagerPaymentError.PaymentFailure(new PaymentError.PayerNotFound()));
        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            return Result<PaymentOutcome, ManagerPaymentError>.Failure(
                new ManagerPaymentError.PaymentFailure(new PaymentError.PayerUnavailable()));

        var calculation = bound.Calculation;
        var charge = await paymentManager.SettleBoundCommissionAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            Money.FromMinorUnits(calculation.PayeeGrossMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(bound, bookingId),
            commissionBindingId,
            ct);
        if (!charge.TryGetValue(out var outcome))
        {
            charge.TryGetError(out var rejection);
            return Result<PaymentOutcome, ManagerPaymentError>.Failure(
                new ManagerPaymentError.PaymentFailure(rejection!.ToPaymentError()));
        }

        commissionService.BindPaymentIntent(bound.Binding, outcome.TransactionId);
        var transaction = SettlementTransactionEntity.CreateBound(
            payerId,
            payeeId,
            outcome.TransactionId,
            calculation,
            TransactionStatus.Pending,
            bookingId,
            commissionBindingId);
        await transactionRepository.AddAsync(transaction, ct);

        if (!outcome.RequiresAction && transaction.Complete(timeProvider.GetUtcNow().UtcDateTime).IsSuccess)
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return Result<PaymentOutcome, ManagerPaymentError>.Success(outcome);
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

    public async Task<Result<CheckoutSession, HoldSessionError>> CreateBoundCommissionHoldSessionAsync(
        Guid payerId,
        Money gross,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var boundIntent = await commissionService.FindBoundPaymentIntentAsync(commissionBindingId, ct);
        boundIntent.TryGetValue(out var boundIntentId);

        var authorized = await commissionService.CalculateBoundAsync(
            commissionBindingId,
            externalReference,
            payerId.ToString(),
            gross,
            boundIntentId,
            stripeSetupIntentId,
            ct);
        if (!authorized.TryGetValue(out var bound))
        {
            authorized.TryGetError(out var error);
            return Result<CheckoutSession, HoldSessionError>.Failure(new HoldSessionError.CommissionFailure(error!));
        }

        var customer = await ResolveStripeCustomerAsync(payerId, ct);
        if (!customer.TryGetValue(out var stripeCustomerId))
        {
            customer.TryGetError(out var error);
            return Result<CheckoutSession, HoldSessionError>.Failure(new HoldSessionError.PaymentFailure(error!));
        }

        if (boundIntentId is not null)
            return Result<CheckoutSession, HoldSessionError>.Success(
                await stripeAccountClient.GetHoldSessionAsync(stripeCustomerId, boundIntentId, ct));

        var calculation = bound.Calculation;
        var checkoutSession = await stripeAccountClient.CreateBoundCommissionHoldSessionAsync(
            stripeCustomerId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            new Dictionary<string, string>(CommissionMetadata(bound, null)).Merge(metadata),
            commissionBindingId,
            ct);
        if (string.IsNullOrWhiteSpace(checkoutSession.StripeIntentId))
            throw new InvalidOperationException("Stripe hold session response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(bound.Binding, checkoutSession.StripeIntentId);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<CheckoutSession, HoldSessionError>.Success(checkoutSession);
    }

    public async Task<string> FindHeldIntentAsync(Guid payerId, int applicationId, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct);
        var stripeCustomerId = account?.StripeCustomerId
            ?? throw new NotFoundException($"No Stripe customer for payer {payerId}");
        return await stripeHoldClient.FindHeldIntentAsync(stripeCustomerId, applicationId, ct);
    }

    public async Task<Money> GetTicketRevenueAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        Money.FromMinorUnits(
            await transactionRepository.GetCompletedTicketRevenueAsync(payeeId, period, ct),
            Currency.Gbp);

    public async Task<Money> GetSettlementPayoutsAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        Money.FromMinorUnits(
            await transactionRepository.GetCompletedSettlementPayoutsAsync(payeeId, period, ct),
            Currency.Gbp);

    public Task<IReadOnlyList<MonthlyPaymentTotal>> GetTicketRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        transactionRepository.GetCompletedTicketRevenueByMonthAsync(payeeId, period, ct);

    public Task<IReadOnlyList<MonthlyPaymentTotal>> GetSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        transactionRepository.GetCompletedSettlementPayoutsByMonthAsync(payeeId, period, ct);

    public Task<IReadOnlyList<SettlementSummary>> GetRecentSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default) =>
        transactionRepository.GetRecentCompletedSettlementsAsync(ownerId, take, ct);

    public async Task<Result<Option<Refund>, SettlementRefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        Money gross,
        string? reason = null,
        CancellationToken ct = default)
    {
        var grossMinor = gross.ToMinorUnits();
        var settlement = await transactionRepository.GetSettlementWithRefundsByBookingIdAsync(bookingId, ct);
        if (settlement is null)
            return Result<Option<Refund>, SettlementRefundError>.Success(Option.None<Refund>());
        if (settlement.CommissionBindingId is null)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.CommissionBindingNotFound());
        if (gross.Currency != settlement.Currency)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.CurrencyMismatch());
        if (settlement.Status != TransactionStatus.Complete)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.SettlementNotRefundable());
        if (grossMinor <= 0)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.AmountMustBePositive());

        var grossAlreadyRefunded = settlement.Refunds
            .Where(refund => refund.CountsTowardCumulative)
            .Sum(refund => refund.GrossRefundedMinor);
        var cumulativeGrossRefund = checked(grossAlreadyRefunded + grossMinor);
        if (cumulativeGrossRefund > settlement.PayeeGrossMinor)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.AmountExceedsRemaining());

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
            settlement.Refunds.Where(refund => refund.CountsTowardCumulative).Sum(refund => refund.CommissionRefundedMinor));
        var commissionVatReversedMinor = checked(
            cumulativeVatReversal -
            settlement.Refunds.Where(refund => refund.CountsTowardCumulative).Sum(refund => refund.CommissionVatReversedMinor));
        var payerTotalRefundMinor = checked(grossMinor + commissionRefundMinor);

        if (!await transactionRepository.TryReserveSettlementRefundGrossAsync(settlement.Id, grossMinor, ct))
            return await ReservationConflictAsync(bookingId, grossMinor, ct);

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
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.PaymentFailure(error!));
        }

        if (settlement.CompleteRefund(reservation, completedRefund.RefundId, timeProvider.GetUtcNow()).IsFailure)
            throw new InvalidOperationException("Settlement refund reservation could not be completed.");

        await ledger.StageAsync(
            LedgerPostings.DirectSettlementRefund(
                settlement.PayerId,
                settlement.PayeeId,
                grossMinor.ToMoney(settlement.Currency),
                checked(commissionRefundMinor - commissionVatReversedMinor).ToMoney(settlement.Currency),
                commissionVatReversedMinor.ToMoney(settlement.Currency),
                settlement.BookingId,
                settlement.PaymentIntentId,
                completedRefund.RefundId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Option<Refund>, SettlementRefundError>.Success(Option.Some(completedRefund));
    }

    private async Task<Result<Option<Refund>, SettlementRefundError>> ReservationConflictAsync(
        int bookingId,
        long grossMinor,
        CancellationToken ct)
    {
        var current = await transactionRepository.GetSettlementWithRefundsByBookingIdAsync(bookingId, ct);
        if (current is null)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.SettlementNotFound());
        if (current.Status != TransactionStatus.Complete)
            return Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.SettlementNotRefundable());
        return checked(current.RefundedGrossMinor + grossMinor) > current.PayeeGrossMinor
            ? Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.AmountExceedsRemaining())
            : Result<Option<Refund>, SettlementRefundError>.Failure(new SettlementRefundError.Conflict());
    }

    private async Task<Result<string, PaymentError>> ResolveStripeCustomerAsync(Guid ownerId, CancellationToken ct)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct);
        if (account is null)
            return Result<string, PaymentError>.Failure(new PaymentError.PayerNotFound());
        if (account.StripeCustomerId is not null)
            return Result<string, PaymentError>.Success(account.StripeCustomerId);

        await stripeAccountClient.ProvisionCustomerAsync(ownerId, account.Email, ct);
        var refreshed = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct);
        var stripeCustomerId = refreshed?.StripeCustomerId;
        if (stripeCustomerId is null)
            throw new InvalidOperationException("Failed to provision Stripe customer.");
        return Result<string, PaymentError>.Success(stripeCustomerId);
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
