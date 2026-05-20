using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Shared.Exceptions;
using FluentResults;

namespace Concertable.Payment.Infrastructure;

internal class CustomerPaymentService : ICustomerPaymentService
{
    private readonly IPaymentManager paymentManager;
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public CustomerPaymentService(
        IPaymentManager paymentManager,
        IStripeAccountClient stripeAccountClient,
        IPayoutAccountRepository payoutAccountRepository)
    {
        this.paymentManager = paymentManager;
        this.stripeAccountClient = stripeAccountClient;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task<Result<PaymentResponse>> PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        IDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        return await paymentManager.ChargeAsync(new ChargeRequest
        {
            PayerId = payerId,
            PayerEmail = account.Email,
            PayeeId = payeeId,
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            Metadata = metadata,
            Session = PaymentSession.OnSession
        }, ct);
    }

    public async Task<CheckoutSession> CreatePaymentSessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        var stripeCustomerId = await EnsureStripeCustomerAsync(account, ct);

        var mergedMetadata = new Dictionary<string, string>
        {
            ["fromUserId"] = payerId.ToString(),
            ["fromUserEmail"] = account.Email
        }
        .Merge(metadata);

        return await stripeAccountClient.CreatePaymentSessionAsync(stripeCustomerId, mergedMetadata, ct);
    }

    private async Task<string> EnsureStripeCustomerAsync(PayoutAccountEntity account, CancellationToken ct)
    {
        if (account.StripeCustomerId is not null)
            return account.StripeCustomerId;

        await stripeAccountClient.ProvisionCustomerAsync(account.UserId, account.Email, ct);

        var refreshed = await payoutAccountRepository.GetByUserIdAsync(account.UserId, ct);
        return refreshed?.StripeCustomerId
            ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
    }
}
