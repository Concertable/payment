using System.Net;
using Concertable.E2ETests;
using Stripe;

namespace Concertable.Payment.E2ETests.Helpers.UnitTests;

public sealed class StripeFixtureTests
{
    private const string CustomerId = "cus_test";
    private const string PaymentMethodId = "pm_test";
    private const string PaymentIntentId = "pi_test";

    private readonly StubStripeHttpClient httpClient;
    private readonly StripeFixture fixture;

    public StripeFixtureTests()
    {
        this.httpClient = new StubStripeHttpClient();
        this.fixture = new StripeFixture(new StripeClient("sk_test_fake", httpClient: this.httpClient));
    }

    [Fact]
    public async Task EnsureNoCardsAttachedAsync_DetachReportsAlreadyDetached_CompletesWhenPostconditionIsMet()
    {
        this.httpClient.Enqueue(HttpStatusCode.OK, ListResponse(CustomerId));
        this.httpClient.Enqueue(HttpStatusCode.BadRequest, ErrorResponse("already detached"));
        this.httpClient.Enqueue(HttpStatusCode.OK, PaymentMethodResponse(null));

        await this.fixture.EnsureNoCardsAttachedAsync(CustomerId);

        Assert.Equal(3, this.httpClient.Requests.Count);
        Assert.Equal(HttpMethod.Get, this.httpClient.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, this.httpClient.Requests[1].Method);
        Assert.Equal(HttpMethod.Get, this.httpClient.Requests[2].Method);
    }

    [Fact]
    public async Task EnsureNoCardsAttachedAsync_DetachFailsWhileStillAttached_RethrowsFailure()
    {
        this.httpClient.Enqueue(HttpStatusCode.OK, ListResponse(CustomerId));
        this.httpClient.Enqueue(HttpStatusCode.BadRequest, ErrorResponse("detach failed"));
        this.httpClient.Enqueue(HttpStatusCode.OK, PaymentMethodResponse(CustomerId));

        var exception = await Assert.ThrowsAsync<StripeException>(
            () => this.fixture.EnsureNoCardsAttachedAsync(CustomerId));

        Assert.Equal("detach failed", exception.Message);
    }

    [Fact]
    public async Task GetCapturedHoldAsync_ExactCapturedIntentMatches_ReturnsIntent()
    {
        this.httpClient.Enqueue(HttpStatusCode.OK, PaymentIntentResponse(26_000, "succeeded"));

        var result = await this.fixture.GetCapturedHoldAsync(PaymentIntentId, 260m);

        Assert.NotNull(result);
        Assert.Equal(PaymentIntentId, result.Id);
        Assert.Single(this.httpClient.Requests);
        Assert.Equal($"/v1/payment_intents/{PaymentIntentId}", this.httpClient.Requests[0].Uri.AbsolutePath);
    }

    [Theory]
    [InlineData(25_000, "succeeded")]
    [InlineData(26_000, "requires_capture")]
    public async Task GetCapturedHoldAsync_IntentDoesNotMatch_ReturnsNull(long amount, string status)
    {
        this.httpClient.Enqueue(HttpStatusCode.OK, PaymentIntentResponse(amount, status));

        var result = await this.fixture.GetCapturedHoldAsync(PaymentIntentId, 260m);

        Assert.Null(result);
    }

    private static string ListResponse(string customerId) => $$"""
        {
          "object": "list",
          "data": [{{PaymentMethodResponse(customerId)}}],
          "has_more": false,
          "url": "/v1/payment_methods"
        }
        """;

    private static string PaymentMethodResponse(string? customerId) => $$"""
        {
          "id": "{{PaymentMethodId}}",
          "object": "payment_method",
          "customer": {{(customerId is null ? "null" : $"\"{customerId}\"")}},
          "type": "card"
        }
        """;

    private static string ErrorResponse(string message) => $$"""
        {
          "error": {
            "type": "invalid_request_error",
            "message": "{{message}}"
          }
        }
        """;

    private static string PaymentIntentResponse(long amount, string status) => $$"""
        {
          "id": "{{PaymentIntentId}}",
          "object": "payment_intent",
          "amount": {{amount}},
          "currency": "gbp",
          "status": "{{status}}"
        }
        """;

    private sealed class StubStripeHttpClient : IHttpClient
    {
        private readonly Queue<StripeResponse> responses = new();

        public List<StripeRequest> Requests { get; } = [];

        public void Enqueue(HttpStatusCode statusCode, string content)
        {
            using var response = new HttpResponseMessage();
            this.responses.Enqueue(new StripeResponse(statusCode, response.Headers, content));
        }

        public Task<StripeResponse> MakeRequestAsync(
            StripeRequest request,
            CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult(this.responses.Dequeue());
        }

        public Task<StripeStreamedResponse> MakeStreamingRequestAsync(
            StripeRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
