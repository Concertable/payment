using System.Text.Json;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentOperationReferenceTests
{
    [Fact]
    public void Constructor_TrimsValues()
    {
        var reference = new PaymentOperationReference(" escrow ", " order:42 ");

        Assert.Equal("escrow", reference.OperationType);
        Assert.Equal("order:42", reference.ClientReference);
    }

    [Theory]
    [InlineData("", "order:42")]
    [InlineData("escrow", "")]
    [InlineData(" ", "order:42")]
    [InlineData("escrow", " ")]
    public void Constructor_EmptyValue_Throws(string operationType, string clientReference) =>
        Assert.ThrowsAny<ArgumentException>(() => new PaymentOperationReference(operationType, clientReference));

    [Fact]
    public void Constructor_OperationTypeTooLong_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentOperationReference(
            new string('a', PaymentOperationReference.MaxOperationTypeLength + 1),
            "order:42"));

    [Fact]
    public void Constructor_ClientReferenceTooLong_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentOperationReference(
            "escrow",
            new string('a', PaymentOperationReference.MaxClientReferenceLength + 1)));

    [Fact]
    public void DefaultValue_EnsureValid_Throws() =>
        Assert.ThrowsAny<ArgumentException>(() => default(PaymentOperationReference).EnsureValid());

    [Fact]
    public void Deserialize_SerializedReference_PreservesValues()
    {
        var reference = new PaymentOperationReference("escrow", "booking:48");

        var json = JsonSerializer.Serialize(reference);
        var deserialized = JsonSerializer.Deserialize<PaymentOperationReference>(json);

        Assert.Equal(reference, deserialized);
    }
}
