using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Application.Provider;
using Concertable.Payment.Domain.ProviderContract;
using Concertable.Payment.Infrastructure.Mappers;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripeSessionClient : IStripeSessionClient
{
    private readonly PaymentIntentService paymentIntentService;
    private readonly SetupIntentService setupIntentService;
    private readonly CustomerSessionService customerSessionService;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<StripeSessionClient> logger;

    public StripeSessionClient(
        PaymentIntentService paymentIntentService,
        SetupIntentService setupIntentService,
        CustomerSessionService customerSessionService,
        TimeProvider timeProvider,
        ILogger<StripeSessionClient> logger)
    {
        this.paymentIntentService = paymentIntentService;
        this.setupIntentService = setupIntentService;
        this.customerSessionService = customerSessionService;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
        PaymentSessionProviderRequest request,
        StripeIdempotencyKey idempotencyKey,
        CancellationToken ct = default)
    {
        try
        {
            var providerIdempotencyKey = idempotencyKey.ToString();
            return request.SessionKind switch
            {
                PaymentSessionKind.Payment or PaymentSessionKind.Authorization =>
                    ToResult(await paymentIntentService.CreateAsync(
                        PaymentIntentOptions(request),
                        new RequestOptions { IdempotencyKey = providerIdempotencyKey },
                        ct)),
                PaymentSessionKind.PaymentMethodSetup or PaymentSessionKind.PaymentMethodVerification =>
                    ToResult(await setupIntentService.CreateAsync(
                        SetupIntentOptions(request),
                        new RequestOptions { IdempotencyKey = providerIdempotencyKey },
                        ct)),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.SessionKind, null)
            };
        }
        catch (StripeException ex) when (ex.StripeError?.PaymentIntent is { } intent)
        {
            return ToResult(intent);
        }
        catch (StripeException ex) when (ex.StripeError?.SetupIntent is { } intent)
        {
            return ToResult(intent);
        }
        catch (StripeException ex)
        {
            logger.StripeSessionCallFailed(nameof(CreateAsync), request.ProviderCustomerId, ex);
            return new PaymentOperationError.ProviderUnavailable();
        }
    }

    public async Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        try
        {
            return providerObjectKind switch
            {
                PaymentSessionProviderObjectKind.PaymentIntent =>
                    ToResult(await paymentIntentService.GetAsync(
                        providerObjectId,
                        new PaymentIntentGetOptions { Expand = ["latest_charge"] },
                        cancellationToken: ct)),
                PaymentSessionProviderObjectKind.SetupIntent =>
                    ToResult(await setupIntentService.GetAsync(providerObjectId, cancellationToken: ct)),
                _ => throw new ArgumentOutOfRangeException(nameof(providerObjectKind), providerObjectKind, null)
            };
        }
        catch (StripeException ex)
        {
            logger.StripeSessionCallFailed(nameof(RetrieveAsync), providerObjectId, ex);
            return new PaymentOperationError.ProviderUnavailable();
        }
    }

    public async Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        try
        {
            return providerObjectKind switch
            {
                PaymentSessionProviderObjectKind.PaymentIntent =>
                    ToResult(await paymentIntentService.CancelAsync(providerObjectId, cancellationToken: ct)),
                PaymentSessionProviderObjectKind.SetupIntent =>
                    ToResult(await setupIntentService.CancelAsync(providerObjectId, cancellationToken: ct)),
                _ => throw new ArgumentOutOfRangeException(nameof(providerObjectKind), providerObjectKind, null)
            };
        }
        catch (StripeException ex)
        {
            logger.StripeSessionCallFailed(nameof(CancelAsync), providerObjectId, ex);
            return new PaymentOperationError.ProviderUnavailable();
        }
    }

    public async Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default)
    {
        try
        {
            var session = await customerSessionService.CreateAsync(
                new CustomerSessionCreateOptions
                {
                    Customer = providerCustomerId,
                    Components = new CustomerSessionComponentsOptions
                    {
                        PaymentElement = new CustomerSessionComponentsPaymentElementOptions
                        {
                            Enabled = true,
                            Features = new CustomerSessionComponentsPaymentElementFeaturesOptions
                            {
                                PaymentMethodSave = "enabled",
                                PaymentMethodRemove = "enabled",
                                PaymentMethodRedisplay = "enabled",
                                PaymentMethodAllowRedisplayFilters = ["always"]
                            }
                        }
                    }
                },
                cancellationToken: ct);
            return session.ClientSecret;
        }
        catch (StripeException ex)
        {
            logger.StripeSessionCallFailed(nameof(CreateCustomerSessionAsync), providerCustomerId, ex);
            return new PaymentOperationError.ProviderUnavailable();
        }
    }

    private static PaymentIntentCreateOptions PaymentIntentOptions(PaymentSessionProviderRequest request)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = request.AmountMinor,
            Currency = request.Currency?.ToString().ToLowerInvariant(),
            Customer = request.ProviderCustomerId,
            CaptureMethod = request.SessionKind == PaymentSessionKind.Authorization ? "manual" : "automatic",
            PaymentMethod = request.PaymentMethodId,
            Confirm = request.Session == PaymentSession.OffSession ? true : null,
            OffSession = request.Session == PaymentSession.OffSession ? true : null,
            SetupFutureUsage = "off_session",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            },
            Metadata = request.Metadata.ToDictionary()
        };

        if (request.FundsRouting == PaymentSessionFundsRouting.Destination)
        {
            options.OnBehalfOf = request.ProviderConnectedAccountId;
            options.TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = request.ProviderConnectedAccountId
            };
        }

        return options;
    }

    private static SetupIntentCreateOptions SetupIntentOptions(PaymentSessionProviderRequest request) =>
        new()
        {
            Customer = request.ProviderCustomerId,
            Usage = request.Session.ToStripeUsage(),
            AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            },
            Metadata = request.Metadata.ToDictionary()
        };

    private ProviderSession ToResult(PaymentIntent intent) =>
        new(
            PaymentSessionProviderObjectKind.PaymentIntent,
            intent.Id,
            intent.Status,
            timeProvider.GetUtcNow(),
            CaptureBefore(intent),
            Classify(intent.LastPaymentError),
            false,
            intent.Status is not ("succeeded" or "canceled"),
            intent.ClientSecret,
            intent.PaymentMethodId,
            null,
            intent.LastPaymentError?.Code,
            intent.LastPaymentError?.Message);

    private static DateTimeOffset? CaptureBefore(PaymentIntent intent)
    {
        DateTime? captureBefore = intent.LatestCharge?.PaymentMethodDetails?.Card?.CaptureBefore
            ?? intent.LatestCharge?.PaymentMethodDetails?.CardPresent?.CaptureBefore;
        return captureBefore is null || captureBefore.Value == DateTime.UnixEpoch
            ? null
            : new DateTimeOffset(captureBefore.Value);
    }

    private ProviderSession ToResult(SetupIntent intent) =>
        new(
            PaymentSessionProviderObjectKind.SetupIntent,
            intent.Id,
            intent.Status,
            timeProvider.GetUtcNow(),
            null,
            Classify(intent.LastSetupError),
            false,
            intent.Status is not ("succeeded" or "canceled"),
            intent.ClientSecret,
            intent.PaymentMethodId,
            null,
            intent.LastSetupError?.Code,
            intent.LastSetupError?.Message);

    private static ProviderFailureClassification? Classify(StripeError? error) =>
        error is not null
        && (string.Equals(error.Type, "card_error", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(error.DeclineCode))
            ? ProviderFailureClassification.Declined
            : null;
}
