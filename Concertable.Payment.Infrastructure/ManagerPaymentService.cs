using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Shared.Exceptions;
using FluentResults;

namespace Concertable.Payment.Infrastructure;

internal class ManagerPaymentService : IManagerPaymentService
{
    private readonly IPaymentManager paymentManager;
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly IStripeHoldClient stripeHoldClient;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly ITransactionRepository transactionRepository;

    public ManagerPaymentService(
        IPaymentManager paymentManager,
        IStripeAccountClient stripeAccountClient,
        IStripeHoldClient stripeHoldClient,
        IPayoutAccountRepository payoutAccountRepository,
        ITransactionRepository transactionRepository)
    {
        this.paymentManager = paymentManager;
        this.stripeAccountClient = stripeAccountClient;
        this.stripeHoldClient = stripeHoldClient;
        this.payoutAccountRepository = payoutAccountRepository;
        this.transactionRepository = transactionRepository;
    }

    public async Task<Result<PaymentResponse>> PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        var payer = await payoutAccountRepository.GetByUserIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        if (session == PaymentSession.OffSession && payer.StripeCustomerId is null)
            throw new BadRequestException("Stripe customer setup is required for off-session payments.");

        var charge = await paymentManager.ChargeAsync(new ChargeRequest
        {
            PayerId = payerId,
            PayerEmail = payer.Email,
            PayeeId = payeeId,
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = TransactionTypes.Settlement,
                ["bookingId"] = bookingId.ToString()
            },
            Session = session
        }, ct);

        if (charge.IsFailed)
            return charge;

        if (string.IsNullOrEmpty(charge.Value.TransactionId))
            return Result.Fail("Stripe charge response missing PaymentIntent id.");

        var transaction = SettlementTransactionEntity.Create(
            payerId,
            payeeId,
            charge.Value.TransactionId,
            (long)(amount * 100),
            TransactionStatus.Pending,
            bookingId);

        await transactionRepository.CreateAsync(transaction);

        if (!charge.Value.RequiresAction)
        {
            transaction.Complete();
            await transactionRepository.SaveChangesAsync();
        }

        return charge;
    }

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        return await stripeAccountClient.CreateSetupSessionAsync(stripeCustomerId, metadata, ct);
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        return await stripeAccountClient.CreateVerifySessionAsync(stripeCustomerId, metadata, ct);
    }

    public async Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        decimal amount,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var stripeCustomerId = await EnsureStripeCustomerAsync(payerId, ct);
        return await stripeAccountClient.CreateHoldSessionAsync(stripeCustomerId, amount, metadata, ct);
    }

    public async Task<string> FindHeldIntentAsync(
        Guid payerId,
        int applicationId,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(payerId, ct);
        var stripeCustomerId = account?.StripeCustomerId
            ?? throw new NotFoundException($"No Stripe customer for payer {payerId}");
        return await stripeHoldClient.FindHeldIntentAsync(stripeCustomerId, applicationId, ct);
    }

    private async Task<string> EnsureStripeCustomerAsync(Guid userId, CancellationToken ct)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new NotFoundException($"Payout account not found for userId {userId}");

        if (account.StripeCustomerId is not null)
            return account.StripeCustomerId;

        await stripeAccountClient.ProvisionCustomerAsync(userId, account.Email, ct);

        var refreshed = await payoutAccountRepository.GetByUserIdAsync(userId, ct);
        return refreshed?.StripeCustomerId
            ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
    }
}
