using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Exceptions;
using Concertable.Payment.Infrastructure.Repositories;
using FluentResults;

namespace Concertable.Payment.Infrastructure;

internal class CustomerPaymentService : ICustomerPaymentService
{
    private readonly IPaymentManager paymentManager;
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly IConcertPayeeRepository concertPayeeRepository;

    public CustomerPaymentService(
        IPaymentManager paymentManager,
        IStripeAccountClient stripeAccountClient,
        IPayoutAccountRepository payoutAccountRepository,
        IConcertPayeeRepository concertPayeeRepository)
    {
        this.paymentManager = paymentManager;
        this.stripeAccountClient = stripeAccountClient;
        this.payoutAccountRepository = payoutAccountRepository;
        this.concertPayeeRepository = concertPayeeRepository;
    }

    public async Task<Result<PaymentResponse>> PayAsync(
        Guid payerId,
        int concertId,
        decimal amount,
        IDictionary<string, string> metadata,
        string paymentMethodId,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        var payeeUserId = await concertPayeeRepository.GetPayeeUserIdAsync(concertId, ct);

        return await paymentManager.ChargeAsync(new ChargeRequest
        {
            PayerId = payerId,
            PayerEmail = account.Email,
            PayeeId = payeeUserId,
            Amount = amount,
            PaymentMethodId = paymentMethodId,
            Metadata = metadata,
            Session = PaymentSession.OnSession
        }, ct);
    }

    public async Task<CheckoutSession> CreatePaymentSessionAsync(
        Guid payerId,
        int concertId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(payerId, ct)
            ?? throw new NotFoundException($"Payout account not found for payer {payerId}");

        var payeeUserId = await concertPayeeRepository.GetPayeeUserIdAsync(concertId, ct);
        var stripeCustomerId = await EnsureStripeCustomerAsync(account, ct);

        var mergedMetadata = new Dictionary<string, string>
        {
            ["fromUserId"] = payerId.ToString(),
            ["fromUserEmail"] = account.Email,
            ["toUserId"] = payeeUserId.ToString()
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
