extern alias PaymentClient;

using System.Net;
using System.Net.Sockets;
using Concertable.Kernel.Auth;
using Concertable.Payment.Web;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClientExtensions = PaymentClient::Concertable.Payment.Client.Extensions.ServiceCollectionExtensions;
using ClientGrpc = PaymentClient::Concertable.Payment.Grpc;
using ServerGrpc = Concertable.Payment.Grpc;

namespace Concertable.Payment.IntegrationTests;

public sealed class PaymentTransportTests
{
    [Fact]
    public async Task PaymentClient_UsesDedicatedHttp2Endpoint_WithBearerCredentials()
    {
        var httpPort = GetAvailablePort();
        var grpcPort = GetAvailablePort();
        while (grpcPort == httpPort)
            grpcPort = GetAvailablePort();
        var authorization = new AuthorizationCapture();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentTransport:GrpcPort"] = grpcPort.ToString()
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{httpPort}", $"http://127.0.0.1:{grpcPort}");
        builder.ConfigurePaymentTransport();
        builder.Services.AddSingleton(authorization);
        builder.Services.AddGrpc();

        await using var app = builder.Build();
        app.MapGet("/transport-probe", () => Results.Ok());
        app.MapGrpcService<PayoutAccountProbe>();
        await app.StartAsync();

        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{httpPort}") };
            using var response = await httpClient.GetAsync("/transport-probe");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpVersion.Version11, response.Version);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["services:payment-web:grpc:0"] = $"http://127.0.0.1:{grpcPort}",
                    ["services:payment-web:https:0"] = $"http://127.0.0.1:{httpPort}"
                })
                .Build();
            await using var serviceProvider = ClientExtensions.AddPaymentClient(
                    new ServiceCollection().AddSingleton<ITokenService>(new StubTokenService()),
                    configuration)
                .BuildServiceProvider();
            var client = serviceProvider.GetRequiredService<ClientGrpc.PayoutAccount.PayoutAccountClient>();

            var account = await client.GetAccountStatusAsync(new ClientGrpc.PayoutOwnerRequest
            {
                OwnerId = Guid.NewGuid().ToString()
            });

            Assert.Equal(ClientGrpc.PayoutAccountStatusType.PayoutVerified, account.Status);
            Assert.Equal("Bearer payment-test-token", authorization.Value);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class AuthorizationCapture
    {
        public string? Value { get; set; }
    }

    private sealed class StubTokenService : ITokenService
    {
        public Task<string> GetTokenAsync(string scope, CancellationToken ct = default)
        {
            Assert.Equal("payment:write", scope);
            return Task.FromResult("payment-test-token");
        }
    }

    private sealed class PayoutAccountProbe(AuthorizationCapture authorization)
        : ServerGrpc.PayoutAccount.PayoutAccountBase
    {
        public override Task<ServerGrpc.AccountStatusResponse> GetAccountStatus(
            ServerGrpc.PayoutOwnerRequest request,
            ServerCallContext context)
        {
            authorization.Value = context.RequestHeaders.GetValue("authorization");
            return Task.FromResult(new ServerGrpc.AccountStatusResponse
            {
                Status = ServerGrpc.PayoutAccountStatusType.PayoutVerified
            });
        }
    }
}
