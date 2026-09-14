namespace Concertable.Payment.UnitTests.Mappers;

public sealed class TransactionMapperTests
{
    private readonly TransactionMapper sut = new();

    [Fact]
    public void ToEntity_WithPaymentDto_ReturnsPaymentTransactionEntity()
    {
        var dto = new PaymentTransactionDto
        {
            PayerId = Guid.NewGuid(),
            PayeeId = Guid.NewGuid(),
            PaymentIntentId = "pi_test",
            OperationType = "purchase",
            ClientReference = "order:1"
        };

        var result = sut.ToEntity(dto);

        Assert.IsType<PaymentTransactionEntity>(result);
    }

    [Fact]
    public void ToEntity_WithSettlementDto_ReturnsSettlementTransactionEntity()
    {
        var dto = new SettlementTransactionDto
        {
            PayerId = Guid.NewGuid(),
            PayeeId = Guid.NewGuid(),
            PaymentIntentId = "pi_test",
            OperationType = "settlement",
            ClientReference = "order:1"
        };

        var result = sut.ToEntity(dto);

        Assert.IsType<SettlementTransactionEntity>(result);
    }

    [Fact]
    public void ToDto_WithPaymentEntity_ReturnsPaymentTransactionDto()
    {
        var entity = PaymentTransactionEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), "pi_test", 0, TransactionStatus.Complete, new("purchase", "order:1"));

        var result = sut.ToDto(entity);

        Assert.IsType<PaymentTransactionDto>(result);
    }

    [Fact]
    public void ToDto_WithSettlementEntity_ReturnsSettlementTransactionDto()
    {
        var entity = SettlementTransactionEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), "pi_test", 0, 0, TransactionStatus.Complete, new("settlement", "order:1"));

        var result = sut.ToDto(entity);

        Assert.IsType<SettlementTransactionDto>(result);
    }
}
