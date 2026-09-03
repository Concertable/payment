using Concertable.DataAccess.Application;
using Concertable.Kernel.ValueObjects;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Handlers;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Reunion;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class FinancialOperationHandlerTests
{
    private readonly Mock<IEscrowService> escrowService;
    private readonly Mock<IFinancialOperationRepository> operationRepository;
    private readonly Mock<IUnitOfWork> unitOfWork;
    private readonly Mock<IBus> bus;
    private readonly Mock<IPaymentOperationResolver> paymentOperationResolver;
    private readonly FakeTimeProvider timeProvider;
    private readonly FinancialOperationHandler sut;

    public FinancialOperationHandlerTests()
    {
        this.escrowService = new Mock<IEscrowService>();
        this.operationRepository = new Mock<IFinancialOperationRepository>();
        this.unitOfWork = new Mock<IUnitOfWork>();
        this.bus = new Mock<IBus>();
        this.paymentOperationResolver = new Mock<IPaymentOperationResolver>();
        this.timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var outbox = new Mock<IOutboxUnitOfWorkBehavior>();
        outbox
            .Setup(behavior => behavior.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, _) => action());

        this.sut = new FinancialOperationHandler(
            escrowService.Object,
            operationRepository.Object,
            unitOfWork.Object,
            bus.Object,
            outbox.Object,
            paymentOperationResolver.Object,
            timeProvider);
    }

    [Fact]
    public async Task HandleAsync_NewCapture_PersistsIntentBeforePaymentAndPublishesSuccess()
    {
        var command = new CaptureEscrowCommand(
            Guid.NewGuid(),
            17,
            Guid.NewGuid(),
            Guid.NewGuid(),
            5000,
            Currency.Gbp,
            "pi_test");
        FinancialOperationEntity? operation = null;
        var sequence = new List<string>();
        operationRepository
            .Setup(repository => repository.GetAsync(command.OperationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialOperationEntity?)null);
        operationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<FinancialOperationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<FinancialOperationEntity, CancellationToken>((value, _) =>
            {
                operation = value;
                sequence.Add("intent");
            })
            .Returns(Task.CompletedTask);
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("save"))
            .Returns(Task.CompletedTask);
        escrowService
            .Setup(service => service.CaptureAsync(
                command.PayerId,
                command.PayeeId,
                Money.FromMinorUnits(command.AmountMinor, command.Currency),
                command.PaymentIntentId,
                command.BookingId,
                command.OperationId,
                It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("payment"))
            .ReturnsAsync(Result<EscrowDeposit, EscrowCaptureError>.Success(
                new EscrowDeposit(1, command.PaymentIntentId, EscrowStatus.Held)));

        await sut.HandleAsync(command, Envelope<CaptureEscrowCommand>());

        Assert.Equal(["intent", "save", "payment"], sequence);
        Assert.NotNull(operation);
        Assert.Equal(FinancialOperationStatus.Succeeded, operation.Status);
        bus.Verify(value => value.PublishAsync(
            It.Is<CaptureEscrowSucceededEvent>(@event =>
                @event.OperationId == command.OperationId && @event.ReferenceId == command.PaymentIntentId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CompletedCapture_ReplaysOutcomeWithoutPayment()
    {
        var command = new CaptureEscrowCommand(
            Guid.NewGuid(),
            17,
            Guid.NewGuid(),
            Guid.NewGuid(),
            5000,
            Currency.Gbp,
            "pi_test");
        FinancialOperationEntity? operation = null;
        operationRepository
            .Setup(repository => repository.GetAsync(command.OperationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => operation);
        operationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<FinancialOperationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<FinancialOperationEntity, CancellationToken>((value, _) => operation = value)
            .Returns(Task.CompletedTask);
        escrowService
            .Setup(service => service.CaptureAsync(
                command.PayerId,
                command.PayeeId,
                Money.FromMinorUnits(command.AmountMinor, command.Currency),
                command.PaymentIntentId,
                command.BookingId,
                command.OperationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EscrowDeposit, EscrowCaptureError>.Success(
                new EscrowDeposit(1, command.PaymentIntentId, EscrowStatus.Held)));

        await sut.HandleAsync(command, Envelope<CaptureEscrowCommand>());
        escrowService.Invocations.Clear();
        bus.Invocations.Clear();

        await sut.HandleAsync(command, Envelope<CaptureEscrowCommand>());

        escrowService.Verify(service => service.CaptureAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Money>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        bus.Verify(value => value.PublishAsync(
            It.Is<CaptureEscrowSucceededEvent>(@event => @event.OperationId == command.OperationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RefundBeforeEscrow_PublishesDeferredAndKeepsPending()
    {
        var command = new RefundEscrowCommand(Guid.NewGuid(), 17, RefundReasonCodes.RequestedByCustomer);
        FinancialOperationEntity? operation = null;
        operationRepository
            .Setup(repository => repository.GetAsync(command.OperationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FinancialOperationEntity?)null);
        operationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<FinancialOperationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<FinancialOperationEntity, CancellationToken>((value, _) => operation = value)
            .Returns(Task.CompletedTask);
        Option<Refund> none = null;
        escrowService
            .Setup(service => service.RefundByBookingIdAsync(
                command.BookingId,
                null,
                command.Reason,
                command.OperationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Option<Refund>, EscrowRefundError>.Success(none));

        await sut.HandleAsync(command, Envelope<RefundEscrowCommand>());

        Assert.NotNull(operation);
        Assert.Equal(FinancialOperationStatus.Pending, operation.Status);
        bus.Verify(value => value.PublishAsync(
            It.Is<RefundEscrowDeferredEvent>(@event => @event.OperationId == command.OperationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MessageEnvelope Envelope<T>() => MessageEnvelope.Create<T>(DateTimeOffset.UtcNow);

}
