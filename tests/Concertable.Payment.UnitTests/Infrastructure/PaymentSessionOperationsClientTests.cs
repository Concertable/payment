extern alias PaymentClient;

using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Grpc.Core;
using PaymentSessionOperationsClient = PaymentClient::Concertable.Payment.Client.Adapters.PaymentSessionOperationsClient;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentSessionOperationsClientTests
{
    [Fact]
    public async Task Operations_ValidRequests_CallGeneratedServiceAndMapResponses()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var invoker = new StubCallInvoker(method => method switch
        {
            "CreateOrReplay" => Descriptor(operationId, attemptId, 1),
            "Retry" => Descriptor(operationId, Guid.CreateVersion7(), 2),
            "GetStatus" => new Proto.PaymentOperationSnapshot
            {
                Identity = Identity(operationId, attemptId, 1),
                State = Proto.PaymentOperationState.RequiresConfirmation,
                TerminalDisposition = Proto.PaymentOperationTerminalDisposition.NonTerminal,
                RetryDisposition = Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt
            },
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        });
        var client = new PaymentSessionOperationsClient(
            new Proto.PaymentSessionOperations.PaymentSessionOperationsClient(invoker));

        var created = await client.CreateOrReplayAsync(new(
            operationId,
            PaymentSessionKind.PaymentMethodSetup,
            PaymentSession.OnSession,
            "setup",
            "account:42",
            ownerId,
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null));
        var retried = await client.RetryAsync(new(operationId, attemptId, 1, ownerId));
        var status = await client.GetStatusAsync(new(operationId, ownerId));

        Assert.True(created.TryGetValue(out var createdDescriptor));
        Assert.Equal(operationId, createdDescriptor.Identity.OperationId);
        Assert.True(retried.TryGetValue(out var retriedDescriptor));
        Assert.Equal(2, retriedDescriptor.Identity.Revision);
        Assert.True(status.TryGetValue(out var snapshot));
        Assert.Equal(PaymentOperationState.RequiresConfirmation, snapshot.State);
        Assert.Equal(
            new[] { "CreateOrReplay", "Retry", "GetStatus" },
            invoker.Calls.Select(call => call.Method));
        Assert.IsType<Proto.PaymentSessionOperationRequest>(invoker.Calls[0].Request);
        Assert.IsType<Proto.PaymentSessionRetryRequest>(invoker.Calls[1].Request);
        Assert.IsType<Proto.PaymentSessionStatusRequest>(invoker.Calls[2].Request);
    }

    private static Proto.PaymentSessionDescriptor Descriptor(
        Guid operationId,
        Guid attemptId,
        long revision) =>
        new()
        {
            Identity = Identity(operationId, attemptId, revision),
            Kind = Proto.PaymentSessionKind.PaymentMethodSetup,
            ClientSecret = "seti_secret"
        };

    private static Proto.PaymentOperationIdentity Identity(
        Guid operationId,
        Guid attemptId,
        long revision) =>
        new()
        {
            OperationId = operationId.ToString("D"),
            AttemptId = attemptId.ToString("D"),
            Revision = revision
        };

    private sealed class StubCallInvoker : CallInvoker
    {
        private readonly Func<string, object> response;

        public StubCallInvoker(Func<string, object> response)
        {
            this.response = response;
        }

        public List<(string Method, object Request)> Calls { get; } = [];

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            Calls.Add((method.Name, request!));
            return new(
                Task.FromResult((TResponse)response(method.Name)),
                Task.FromResult(new Metadata()),
                () => new Status(StatusCode.OK, string.Empty),
                () => new Metadata(),
                () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options) =>
            throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options) =>
            throw new NotSupportedException();
    }
}
