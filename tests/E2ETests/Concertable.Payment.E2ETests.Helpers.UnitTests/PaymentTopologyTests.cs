using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Concertable.Messaging.AzureServiceBus.Options;
using Concertable.Payment.Contracts;
using Concertable.Payment.Hosting;

namespace Concertable.Payment.E2ETests.Helpers.UnitTests;

public sealed class PaymentTopologyTests
{
    [Fact]
    public void AddPaymentTopology_ProvisionsFinancialOperationCommandQueues()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddAzureServiceBus("messaging").Topology().AddPaymentTopology();

        var queues = builder.Resources
            .OfType<AzureServiceBusQueueResource>()
            .Select(resource => resource.Name)
            .ToHashSet();
        var options = new AzureServiceBusOptions();

        Assert.Contains(options.QueueNameFor(PaymentConstants.ServiceName, typeof(CaptureEscrowCommand)), queues);
        Assert.Contains(options.QueueNameFor(PaymentConstants.ServiceName, typeof(DepositEscrowCommand)), queues);
        Assert.Contains(options.QueueNameFor(PaymentConstants.ServiceName, typeof(RefundEscrowCommand)), queues);
    }
}
