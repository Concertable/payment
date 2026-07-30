using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Exceptions;
using FluentResults;
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
    private readonly ILedgerService ledger;
    private readonly IUnitOfWork unitOfWork;
    private readonly Money platformFee;

    public ManagerPaymentService(
        IPaymentManager paymentManager,
        IStripeAccountClient stripeAccountClient,
        IStripeHoldClient stripeHoldClient,
        IPayoutAccountRepository payoutAccountRepository,
        ITransactionRepository transactionRepository,
        ICommissionService commissionService,
        ILedgerService ledger,
        IUnitOfWork unitOfWork,
        IOptions<PlatformFeeOptions> platformFeeOptions)
    {
        this.paymentManager = paymentManager;
        this.stripeAccountClient = stripeAccountClient;
        this.stripeHoldClient = stripeHoldClient;
        this.payoutAccountRepository = payoutAccountRepository;
        this.transactionRepository = transactionRepository;
        this.commissionService = commissionService;
        this.ledger = ledger;
        this.unitOfWork = unitOfWork;
        this.platformFee = Money.Gbp(platformFeeOptions.Value.Fee);
    }

    public async Task<Result<PaymentOutcome>> PayAsync(
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

        if (charge.IsFailed)
            return charge;

        if (string.IsNullOrEmpty(charge.Value.TransactionId))
            return Result.Fail("Stripe charge response missing PaymentIntent id.");

        var transaction = SettlementTransactionEntity.Create(
            payerId,
            payeeId,
            charge.Value.TransactionId,
            (amount + platformFee).ToMinorUnits(),
            platformFee.ToMinorUnits(),
            TransactionStatus.Pending,
            bookingId);

        await transactionRepository.CreateAsync(transaction);

        if (!charge.Value.RequiresAction && transaction.Complete())
        {
            await ledger.StageAsync(LedgerPostings.DirectSettlement(transaction), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return charge;
    }

    public async Task<Result<PaymentOutcome>> PayCommissionAuthorizedAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionAuthorizationId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var existing = await transactionRepository.GetSettlementByCommissionAuthorizationIdAsync(
            commissionAuthorizationId,
            ct);
        if (existing is not null)
            return Result.Ok(new PaymentOutcome
            {
                TransactionId = existing.PaymentIntentId,
                RequiresAction = existing.Status == TransactionStatus.Pending
            });

        var authorized = await commissionService.CalculateAuthorizedAsync(
            commissionAuthorizationId,
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
            return authorized.ToResult<PaymentOutcome>();

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");
        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            throw new BadRequestException("Stripe customer setup is required for off-session payments.");

        var calculation = authorized.Value.Calculation;
        var charge = await paymentManager.SettleAsync(
            payerId,
            payeeId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            Money.FromMinorUnits(calculation.PayeeGrossMinor, calculation.Currency),
            paymentMethodId,
            session,
            CommissionMetadata(authorized.Value, bookingId),
            ct);
        if (charge.IsFailed)
            return charge;
        if (string.IsNullOrEmpty(charge.Value.TransactionId))
            return Result.Fail("Stripe charge response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(
            authorized.Value.Authorization,
            charge.Value.TransactionId);
        var transaction = SettlementTransactionEntity.CreateAuthorized(
            payerId,
            payeeId,
            charge.Value.TransactionId,
            calculation,
            TransactionStatus.Pending,
            bookingId,
            commissionAuthorizationId);
        await transactionRepository.AddAsync(transaction, ct);

        if (!charge.Value.RequiresAction && transaction.Complete())
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

    public async Task<Result<CheckoutSession>> CreateCommissionAuthorizedHoldSessionAsync(
        Guid payerId,
        long grossMinor,
        Currency currency,
        IReadOnlyDictionary<string, string> metadata,
        Guid commissionAuthorizationId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId,
        CancellationToken ct = default)
    {
        var authorized = await commissionService.CalculateAuthorizedAsync(
            commissionAuthorizationId,
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
            return authorized.ToResult<CheckoutSession>();

        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        var calculation = authorized.Value.Calculation;
        var session = await stripeAccountClient.CreateHoldSessionAsync(
            stripeCustomerId,
            Money.FromMinorUnits(calculation.PayerTotalMinor, calculation.Currency),
            new Dictionary<string, string>(CommissionMetadata(authorized.Value, bookingId: null))
                .Merge(metadata),
            ct);
        if (string.IsNullOrWhiteSpace(session.StripeIntentId))
            return Result.Fail("Stripe hold session response missing PaymentIntent id.");

        commissionService.BindPaymentIntent(
            authorized.Value.Authorization,
            session.StripeIntentId);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok(session);
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
        AuthorizedCommission authorized,
        int? bookingId)
    {
        var calculation = authorized.Calculation;
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.Type] = TransactionTypes.Settlement,
            [PaymentMetadataKeys.CommissionAuthorizationId] = authorized.Authorization.Id.ToString(),
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
