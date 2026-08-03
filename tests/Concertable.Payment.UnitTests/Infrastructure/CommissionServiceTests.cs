using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class CommissionServiceTests
{
    private const decimal RatePercentage = 10m;
    private const decimal VatRatePercentage = 20m;
    private const long GrossMinor = 5000;

    private readonly Guid configurationId = Guid.NewGuid();

    private readonly Mock<ICommissionBindingRepository> authorizationRepository = new();
    private readonly Mock<ICommissionConfigurationRepository> configurationRepository = new();
    private readonly CommissionCalculator calculator = new();
    private readonly FakeTimeProvider timeProvider = new();

    [Fact]
    public async Task PreviewAsync_CurrencyMismatch_Fails()
    {
        var result = await BuildService().PreviewAsync(GrossMinor, (Currency)840);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(CommissionError.CurrencyMismatch(), error);
    }

    [Fact]
    public async Task PreviewAsync_MatchingCurrency_ReturnsQuote()
    {
        var expected = calculator.Calculate(GrossMinor, Currency.Gbp, Terms(), Percentage.From(VatRatePercentage));

        var result = await BuildService().PreviewAsync(GrossMinor, Currency.Gbp);

        Assert.True(result.TryGetValue(out var calculation));
        Assert.Equal(configurationId, calculation.CommissionConfigurationId);
        Assert.Equal(expected.PayeeGrossMinor, calculation.GrossMinor);
        Assert.Equal(expected.CommissionGrossMinor, calculation.CommissionMinor);
        Assert.Equal(expected.PayerTotalMinor, calculation.PayerTotalMinor);
    }

    [Fact]
    public async Task CreateOrBindAsync_ReviewedConfigDiffers_ReturnsPricingChanged()
    {
        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, Guid.NewGuid(), null, null, null, null, null);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(CommissionError.PricingChanged(), error);
    }

    [Fact]
    public async Task CreateOrBindAsync_ExistingMatches_ReturnsExisting()
    {
        var existing = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CommissionBindingEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, GrossMinor, 500, 5500);

        Assert.True(result.TryGetValue(out var binding));
        Assert.Equal(existing.Id, binding.BindingId);
    }

    [Fact]
    public async Task CreateOrBindAsync_ExistingIntentDiffers_ReturnsMismatch()
    {
        var existing = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CommissionBindingEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_2", null, null, null, null);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(CommissionError.BindingMismatch(), error);
    }

    [Fact]
    public async Task CreateOrBindAsync_NoExisting_CreatesAndReturns()
    {
        authorizationRepository
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CommissionBindingEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity candidate, CancellationToken _) => candidate);

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, null, null, null);

        Assert.True(result.IsSuccess);
        authorizationRepository.Verify(
            r => r.GetOrCreateAsync(It.IsAny<CommissionBindingEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CalculateBoundAsync_AuthorizationNotFound_Fails()
    {
        var id = Guid.NewGuid();
        authorizationRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity?)null);

        var result = await BuildService().CalculateBoundAsync(
            id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, null, null);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(CommissionError.BindingNotFound(), error);
    }

    [Fact]
    public async Task CalculateBoundAsync_IdentityMismatch_Fails()
    {
        var binding = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:OTHER", "payer:1", Currency.Gbp, GrossMinor, "pi_1", null);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(CommissionError.BindingMismatch(), error);
    }

    [Fact]
    public async Task CalculateBoundAsync_BoundIntentDiffersFromSupplied_Fails()
    {
        var binding = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, "pi_2", null);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(CommissionError.BindingIntentMismatch(), error);
    }

    [Fact]
    public async Task CalculateBoundAsync_ExactIdentityAndIntentMatch_ReturnsBoundCommission()
    {
        var binding = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var expected = calculator.Calculate(GrossMinor, Currency.Gbp, Terms(), Percentage.From(VatRatePercentage));

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, "pi_1", null);

        Assert.True(result.TryGetValue(out var bound));
        Assert.Same(binding, bound.Binding);
        Assert.Equal(Terms(), bound.Terms);
        Assert.Equal(expected, bound.Calculation);
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_ReturnsBoundIntent()
    {
        var binding = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().FindBoundPaymentIntentAsync(binding.Id);

        Assert.True(result.TryGetValue(out var paymentIntentId));
        Assert.Equal("pi_1", paymentIntentId);
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_NoAuthorization_ReturnsNull()
    {
        var id = Guid.NewGuid();
        authorizationRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity?)null);

        var result = await BuildService().FindBoundPaymentIntentAsync(id);

        Assert.True(result.IsNone);
    }

    private CommissionTerms Terms() =>
        Configuration().Terms;

    private CommissionConfigurationEntity Configuration() =>
        CommissionConfigurationEntity.Create(
            configurationId,
            Percentage.From(RatePercentage),
            timeProvider.GetUtcNow());

    private CommissionBindingEntity Binding(
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId) =>
        CommissionBindingEntity.Create(
            Configuration(), Currency.Gbp, externalReference, payerReference, timeProvider.GetUtcNow(), stripePaymentIntentId);

    private CommissionService BuildService()
    {
        configurationRepository
            .Setup(r => r.GetByIdAsync(configurationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Configuration());

        return new CommissionService(
            authorizationRepository.Object,
            configurationRepository.Object,
            calculator,
            Options.Create(new PlatformCommissionOptions
            {
                ConfigurationId = configurationId,
                RatePercentage = RatePercentage
            }),
            Options.Create(new PlatformCommissionTaxOptions { VatRatePercentage = VatRatePercentage }),
            timeProvider);
    }
}
