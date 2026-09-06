using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Concertable.Messaging.AzureServiceBus.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Concertable.Payment.Hosting;

public static class AppHostExtensions
{
    public static IResourceBuilder<ServiceContainerResource> AddPaymentWeb(
        this IDistributedApplicationBuilder builder,
        string image,
        string digest,
        IResourceBuilder<IResourceWithServiceDiscovery> auth,
        IResourceBuilder<SqlServerDatabaseResource> paymentDb,
        IResourceBuilder<AzureServiceBusResource> asb)
    {
        var httpPorts = $"{PaymentConstants.HttpPort};{PaymentConstants.GrpcPort}";

        return builder.AddContainerImage(PaymentConstants.WebResource, image, digest)
                      .WithHttpEndpoint(targetPort: PaymentConstants.HttpPort, name: "https")
                      .WithHttpEndpoint(targetPort: PaymentConstants.HttpPort, name: "http")
                      .WithHttpEndpoint(targetPort: PaymentConstants.GrpcPort, name: "grpc")
                      .WithReference(paymentDb)
                      .WaitFor(paymentDb)
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .WithEnvironment("PaymentTransport__GrpcPort", PaymentConstants.GrpcPort.ToString())
                      .WithEnvironment("ASPNETCORE_HTTP_PORTS", httpPorts)
                      .WithEnvironment(AzureServiceBusOptions.ServiceNameEnvVar, PaymentConstants.ServiceName)
                      .AddSecrets(builder, "Stripe:SecretKey", "Stripe:WebhookSecret", "ExternalServices:UseRealStripe");
    }

    public static IResourceBuilder<ProjectResource> AddPaymentWeb<TProject>(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<IResourceWithServiceDiscovery> auth,
        IResourceBuilder<SqlServerDatabaseResource> paymentDb,
        IResourceBuilder<AzureServiceBusResource> asb)
        where TProject : IProjectMetadata, new()
    {
        return builder.AddProject<TProject>(PaymentConstants.WebResource)
                      .WithReference(paymentDb)
                      .WaitFor(paymentDb)
                      .WithReference(auth)
                      .WaitFor(auth)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithEnvironment("Auth__Authority", auth.GetEndpoint("https"))
                      .WithEnvironment(AzureServiceBusOptions.ServiceNameEnvVar, PaymentConstants.ServiceName)
                      .AddSecrets(builder, "Stripe:SecretKey", "Stripe:WebhookSecret", "ExternalServices:UseRealStripe");
    }

    public static IResourceBuilder<ProjectResource> AddPaymentWorkers<TProject>(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> paymentDb,
        IResourceBuilder<AzureServiceBusResource> asb)
        where TProject : IProjectMetadata, new()
    {
        return builder.AddProject<TProject>(PaymentConstants.WorkersResource)
                      .WithReference(paymentDb)
                      .WaitFor(paymentDb)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithEnvironment(AzureServiceBusOptions.ServiceNameEnvVar, PaymentConstants.ServiceName)
                      .AddSecrets(builder, "Stripe:SecretKey", "ExternalServices:UseRealStripe");
    }

    public static IResourceBuilder<ServiceContainerResource> AddPaymentWorkers(
        this IDistributedApplicationBuilder builder,
        string image,
        string digest,
        IResourceBuilder<SqlServerDatabaseResource> paymentDb,
        IResourceBuilder<AzureServiceBusResource> asb)
    {
        return builder.AddContainerImage(PaymentConstants.WorkersResource, image, digest)
                      .WithReference(paymentDb)
                      .WaitFor(paymentDb)
                      .WithReference(asb)
                      .WaitFor(asb)
                      .WithEnvironment(AzureServiceBusOptions.ServiceNameEnvVar, PaymentConstants.ServiceName)
                      .AddSecrets(builder, "Stripe:SecretKey", "ExternalServices:UseRealStripe");
    }

    public static void AddStripeCli<TPaymentWeb>(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<TPaymentWeb> paymentWeb)
        where TPaymentWeb : class, IResourceWithServiceDiscovery, IResourceWithEnvironment
    {
        var secretKey = builder.Configuration["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
            return;

        IResource stripeCli = builder.ExecutionContext.IsRunMode
            ? builder.AddExecutable(PaymentConstants.StripeCliResource, "stripe", ".")
                .WithArgs("listen", "--api-key", secretKey, "--skip-verify", "--forward-to",
                    ReferenceExpression.Create($"{paymentWeb.GetEndpoint("https")}/api/webhook"))
                .Resource
            : builder.AddContainer(PaymentConstants.StripeCliResource, "stripe/stripe-cli")
                .WithVolume("stripe-cli-config", "/root/.config/stripe")
                .WithArgs("listen", "--api-key", secretKey, "--forward-to",
                    ReferenceExpression.Create($"{paymentWeb.GetEndpoint("http")}/api/webhook"))
                .Resource;

        var webhookSecret = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var logs = evt.Services.GetRequiredService<ResourceLoggerService>();
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var line in logs.WatchLinesAsync(stripeCli, ct))
                    {
                        var match = Regex.Match(line.Content, @"whsec_\w+");
                        if (match.Success)
                        {
                            webhookSecret.TrySetResult(match.Value);
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    webhookSecret.TrySetCanceled(ct);
                }
            }, ct);
            return Task.CompletedTask;
        });

        paymentWeb.WithEnvironment(async ctx =>
        {
            ctx.EnvironmentVariables["Stripe__WebhookSecret"] =
                await webhookSecret.Task.WaitAsync(TimeSpan.FromSeconds(60));
        });
    }

    private static async IAsyncEnumerable<LogLine> WatchLinesAsync(
        this ResourceLoggerService logs,
        IResource resource,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var batch in logs.WatchAsync(resource).WithCancellation(ct))
            foreach (var line in batch)
                yield return line;
    }
}
