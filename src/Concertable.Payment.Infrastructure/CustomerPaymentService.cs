using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Exceptions;
using FluentResults;

namespace Concertable.Payment.Infrastructure;

internal sealed class CustomerPaymentService : ICustomerPaymentService
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

    public async Task<Result<PaymentOutcome>> PayAsync(
        Guid payerId,
        int concertId,
        Guid payeeId,
        decimal amount,
        IDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
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
        int concertId,
        Guid payeeId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        var stripeCustomerId = await EnsureStripeCustomerAsync(account, ct);

        var mergedMetadata = new Dictionary<string, string>
        {
            ["fromUserId"] = payerId.ToString(),
            ["fromUserEmail"] = account.Email,
            ["toUserId"] = payeeId.ToString()
        }
        .Merge(metadata);

        return await stripeAccountClient.CreatePaymentSessionAsync(stripeCustomerId, mergedMetadata, ct);
    }

    private async Task<string> EnsureStripeCustomerAsync(PayoutAccountEntity account, CancellationToken ct)
    {
        if (account.StripeCustomerId is not null)
            return account.StripeCustomerId;

        await stripeAccountClient.ProvisionCustomerAsync(account.OwnerId, account.Email, ct);

        var refreshed = await payoutAccountRepository.GetByOwnerIdAsync(account.OwnerId, ct);
        return refreshed?.StripeCustomerId
            ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
    }
}
