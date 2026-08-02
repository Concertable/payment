using Concertable.Kernel.ValueObjects;
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
    private const int RateBasisPoints = 1000;
    private const int VatRateBasisPoints = 2000;
    private const string ConfigurationVersion = "2024.1";
    private const long GrossMinor = 5000;

    private readonly Guid configurationId = Guid.NewGuid();
    private readonly Guid previousConfigurationId = Guid.NewGuid();

    private readonly Mock<ICommissionBindingRepository> authorizationRepository = new();
    private readonly CommissionCalculator calculator = new();
    private readonly FakeTimeProvider timeProvider = new();

    [Fact]
    public async Task PreviewAsync_CurrencyMismatch_Fails()
    {
        var result = await BuildService().PreviewAsync(GrossMinor, (Currency)840);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "currency_mismatch");
    }

    [Fact]
    public async Task PreviewAsync_MatchingCurrency_ReturnsQuote()
    {
        var expected = calculator.Calculate(GrossMinor, Terms(), VatRateBasisPoints);

        var result = await BuildService().PreviewAsync(GrossMinor, Currency.Gbp);

        Assert.True(result.IsSuccess);
        Assert.Equal(configurationId, result.Value.CommissionConfigurationId);
        Assert.Equal(expected.PayeeGrossMinor, result.Value.GrossMinor);
        Assert.Equal(expected.CommissionGrossMinor, result.Value.CommissionMinor);
        Assert.Equal(expected.PayerTotalMinor, result.Value.PayerTotalMinor);
    }

    [Fact]
    public async Task CreateOrBindAsync_ReviewedConfigDiffers_ReturnsPricingChanged()
    {
        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, Guid.NewGuid(), null, null, null, null, null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "pricing_changed");
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

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.BindingId);
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

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_mismatch");
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

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_not_found");
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

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_mismatch");
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

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_intent_mismatch");
    }

    [Fact]
    public async Task CalculateBoundAsync_ExactIdentityAndIntentMatch_ReturnsBoundCommission()
    {
        var binding = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var expected = calculator.Calculate(GrossMinor, Terms(), VatRateBasisPoints);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, "pi_1", null);

        Assert.True(result.IsSuccess);
        Assert.Same(binding, result.Value.Binding);
        Assert.Equal(Terms(), result.Value.Terms);
        Assert.Equal(expected, result.Value.Calculation);
    }

    [Fact]
    public async Task CalculateBoundAsync_PreviousConfiguration_UsesBoundRevision()
    {
        var binding = Binding(
            "booking:7",
            "payer:1",
            "pi_1",
            previousConfigurationId);
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, "pi_1", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(previousConfigurationId, result.Value.Terms.ConfigurationId);
        Assert.Equal(2000, result.Value.Terms.RateBasisPoints);
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_ReturnsBoundIntent()
    {
        var binding = Binding("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().FindBoundPaymentIntentAsync(binding.Id);

        Assert.Equal("pi_1", result);
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_NoAuthorization_ReturnsNull()
    {
        var id = Guid.NewGuid();
        authorizationRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity?)null);

        var result = await BuildService().FindBoundPaymentIntentAsync(id);

        Assert.Null(result);
    }

    private CommissionTerms Terms() =>
        new(configurationId, ConfigurationVersion, Currency.Gbp, RateBasisPoints);

    private CommissionBindingEntity Binding(
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        Guid? boundConfigurationId = null) =>
        CommissionBindingEntity.Create(
            boundConfigurationId ?? configurationId,
            externalReference,
            payerReference,
            timeProvider.GetUtcNow(),
            stripePaymentIntentId);

    private CommissionService BuildService()
    {
        return new CommissionService(
            authorizationRepository.Object,
            new CommissionPricingCatalog(Options.Create(new PlatformCommissionOptions
            {
                CurrentConfigurationId = configurationId,
                Configurations =
                [
                    new PlatformCommissionRevisionOptions
                    {
                        Id = previousConfigurationId,
                        Version = "2023.1",
                        Currency = nameof(Currency.Gbp),
                        RateBasisPoints = 2000
                    },
                    new PlatformCommissionRevisionOptions
                    {
                        Id = configurationId,
                        Version = ConfigurationVersion,
                        Currency = nameof(Currency.Gbp),
                        RateBasisPoints = RateBasisPoints
                    }
                ]
            })),
            calculator,
            Options.Create(new PlatformCommissionTaxOptions { VatRateBasisPoints = VatRateBasisPoints }),
            timeProvider);
    }
}
