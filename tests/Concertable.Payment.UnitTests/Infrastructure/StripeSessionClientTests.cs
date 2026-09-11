using System.Net;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Application.Provider;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Domain.ProviderContract;
using Concertable.Payment.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeSessionClientTests
{
    [Fact]
    public async Task CreateAsync_ConfirmationDeclineWithPaymentIntent_ReturnsProviderObservation()
    {
        var httpClient = new StubStripeHttpClient();
        httpClient.Enqueue(HttpStatusCode.PaymentRequired, DeclinedPaymentIntentResponse());
        var sut = CreateClient(httpClient);

        var result = await sut.CreateAsync(
            Request(PaymentSession.OffSession),
            StripeIdempotencyKey.ForSessionAttempt(Guid.CreateVersion7(), Guid.CreateVersion7(), 1));

        Assert.True(result.TryGetValue(out var observation));
        Assert.Equal(PaymentSessionProviderObjectKind.PaymentIntent, observation.ProviderObjectKind);
        Assert.Equal("pi_test", observation.ProviderObjectId);
        Assert.Equal("requires_payment_method", observation.Status);
        Assert.Equal(ProviderFailureClassification.Declined, observation.FailureClassification);
    }

    [Fact]
    public async Task CreateAsync_OnSessionPayment_PreservesOffSessionFutureUsage()
    {
        var httpClient = new StubStripeHttpClient();
        httpClient.Enqueue(HttpStatusCode.OK, PaymentIntentResponse());
        var sut = CreateClient(httpClient);

        var result = await sut.CreateAsync(
            Request(PaymentSession.OnSession),
            StripeIdempotencyKey.ForSessionAttempt(Guid.CreateVersion7(), Guid.CreateVersion7(), 1));

        Assert.True(result.TryGetValue(out _));
        var content = await httpClient.Requests.Single().Content!.ReadAsStringAsync();
        Assert.Contains("setup_future_usage=off_session", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetrieveAsync_ManualAuthorizationWithoutCaptureDeadline_ReturnsProviderObservation()
    {
        var httpClient = new StubStripeHttpClient();
        httpClient.Enqueue(HttpStatusCode.OK, AuthorizedPaymentIntentWithoutCaptureDeadlineResponse());
        var sut = CreateClient(httpClient);

        var result = await sut.RetrieveAsync(PaymentSessionProviderObjectKind.PaymentIntent, "pi_test");

        Assert.True(result.TryGetValue(out var observation));
        Assert.Equal("requires_capture", observation.Status);
        Assert.Null(observation.CaptureBefore);
    }

    [Fact]
    public async Task CreateCustomerSessionAsync_OffersOnlyMethodsTheCustomerConsentedToRedisplay()
    {
        var httpClient = new StubStripeHttpClient();
        httpClient.Enqueue(HttpStatusCode.OK, CustomerSessionResponse());
        var sut = CreateClient(httpClient);

        var result = await sut.CreateCustomerSessionAsync("cus_test");

        Assert.True(result.TryGetValue(out _));
        var content = await httpClient.Requests.Single().Content!.ReadAsStringAsync();
        Assert.Contains(
            "components[payment_element][features][payment_method_allow_redisplay_filters][0]=always",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("payment_method_allow_redisplay_filters][1]", content, StringComparison.Ordinal);
    }

    private static StripeSessionClient CreateClient(StubStripeHttpClient httpClient)
    {
        var stripeClient = new StripeClient("sk_test_fake", httpClient: httpClient);
        return new(
            new PaymentIntentService(stripeClient),
            new SetupIntentService(stripeClient),
            new CustomerSessionService(stripeClient),
            TimeProvider.System,
            NullLogger<StripeSessionClient>.Instance);
    }

    private static PaymentSessionProviderRequest Request(PaymentSession session) =>
        new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            PaymentSessionKind.Payment,
            session,
            "purchase",
            "order:1",
            1000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Platform,
            session == PaymentSession.OffSession ? "pm_test" : null,
            "cus_test",
            null,
            new Dictionary<string, string>());

    private static string DeclinedPaymentIntentResponse() =>
        """
        {
          "error": {
            "type": "card_error",
            "code": "card_declined",
            "decline_code": "generic_decline",
            "message": "Your card was declined.",
            "payment_intent": {
              "id": "pi_test",
              "object": "payment_intent",
              "amount": 1000,
              "currency": "gbp",
              "status": "requires_payment_method",
              "last_payment_error": {
                "type": "card_error",
                "code": "card_declined",
                "decline_code": "generic_decline",
                "message": "Your card was declined."
              }
            }
          }
        }
        """;

    private static string CustomerSessionResponse() =>
        """
        {
          "object": "customer_session",
          "client_secret": "cuss_test",
          "customer": "cus_test"
        }
        """;

    private static string PaymentIntentResponse() =>
        """
        {
          "id": "pi_test",
          "object": "payment_intent",
          "amount": 1000,
          "currency": "gbp",
          "status": "requires_payment_method"
        }
        """;

    private static string AuthorizedPaymentIntentWithoutCaptureDeadlineResponse() =>
        """
        {
          "id": "pi_test",
          "object": "payment_intent",
          "amount": 1000,
          "currency": "gbp",
          "status": "requires_capture",
          "latest_charge": {
            "id": "ch_test",
            "object": "charge",
            "payment_method_details": {
              "type": "card",
              "card": {}
            }
          }
        }
        """;

    private sealed class StubStripeHttpClient : IHttpClient
    {
        private readonly Queue<StripeResponse> responses = new();

        public List<StripeRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode statusCode, string content)
        {
            using var response = new HttpResponseMessage();
            responses.Enqueue(new StripeResponse(statusCode, response.Headers, content));
        }

        public Task<StripeResponse> MakeRequestAsync(
            StripeRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }

        public Task<StripeStreamedResponse> MakeStreamingRequestAsync(
            StripeRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
