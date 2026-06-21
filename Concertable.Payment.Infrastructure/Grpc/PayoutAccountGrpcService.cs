using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class PayoutAccountGrpcService : PayoutAccount.PayoutAccountBase
{
    private readonly IPayoutAccountService payoutAccountService;

    public PayoutAccountGrpcService(IPayoutAccountService payoutAccountService)
    {
        this.payoutAccountService = payoutAccountService;
    }

    public override async Task<OnboardingLinkResponse> GetOnboardingLink(PayoutOwnerRequest request, ServerCallContext context)
    {
        var link = await payoutAccountService.GetOnboardingLinkAsync(request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)), context.CancellationToken);
        return new OnboardingLinkResponse { Url = link ?? string.Empty };
    }

    public override async Task<AccountStatusResponse> GetAccountStatus(PayoutOwnerRequest request, ServerCallContext context)
    {
        var status = await payoutAccountService.GetAccountStatusAsync(request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)), context.CancellationToken);
        return new AccountStatusResponse { Status = status.ToProtoStatus() };
    }

    public override async Task<PaymentMethodResponse> GetPaymentMethod(PayoutOwnerRequest request, ServerCallContext context)
    {
        var card = await payoutAccountService.GetPaymentMethodAsync(request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)), context.CancellationToken);
        return card is null
            ? new PaymentMethodResponse { HasCard = false }
            : new PaymentMethodResponse
            {
                HasCard = true,
                Brand = card.Brand,
                Last4 = card.Last4,
                ExpMonth = card.ExpMonth,
                ExpYear = card.ExpYear
            };
    }

    public override async Task<SetupIntentResponse> CreateSetupIntent(PayoutOwnerRequest request, ServerCallContext context)
    {
        var secret = await payoutAccountService.CreateSetupIntentAsync(request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)), context.CancellationToken);
        return new SetupIntentResponse { ClientSecret = secret ?? string.Empty };
    }
}
