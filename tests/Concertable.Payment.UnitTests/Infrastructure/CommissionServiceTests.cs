using System.Reflection;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
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

    private readonly Mock<ICommissionConfigurationRepository> configurationRepository = new();
    private readonly Mock<ICommissionBindingRepository> authorizationRepository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly CommissionCalculator calculator = new();
    private readonly FakeTimeProvider timeProvider = new();

    private readonly CommissionConfigurationEntity configuration;

    public CommissionServiceTests()
    {
        configuration = CommissionConfigurationEntity.Create(
            configurationId, ConfigurationVersion, Currency.Gbp, RateBasisPoints, timeProvider.GetUtcNow());

        configurationRepository
            .Setup(r => r.GetByIdAsync(configurationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

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
        var expected = calculator.Calculate(GrossMinor, Currency.Gbp, RateBasisPoints, VatRateBasisPoints);

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
    public async Task CreateOrBindAsync_ExistingMatches_RebindsWithoutInserting()
    {
        var existing = CommissionBindingEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, GrossMinor, 500, 5500);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.BindingId);
        authorizationRepository.Verify(
            r => r.AddAsync(It.IsAny<CommissionBindingEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrBindAsync_ExistingIntentDiffers_ReturnsMismatch()
    {
        var existing = CommissionBindingEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_2", null, null, null, null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_mismatch");
    }

    [Fact]
    public async Task CreateOrBindAsync_NoExisting_InsertsAndReturns()
    {
        authorizationRepository
            .Setup(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity?)null);

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, null, null, null);

        Assert.True(result.IsSuccess);
        authorizationRepository.Verify(
            r => r.AddAsync(It.IsAny<CommissionBindingEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrBindAsync_ConcurrentInsertRace_RecoversExisting()
    {
        var existing = CommissionBindingEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
        authorizationRepository
            .SetupSequence(r => r.GetByIdentityAsync("booking:7", "payer:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity?)null)
            .ReturnsAsync(existing);
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate identity"));

        var result = await BuildService().CreateOrBindAsync(
            "booking:7", "payer:1", Currency.Gbp, configurationId, "pi_1", null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.BindingId);
    }

    [Fact]
    public async Task CalculateBoundAsync_AuthorizationNotFound_Fails()
    {
        var id = Guid.NewGuid();
        authorizationRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommissionBindingEntity?)null);

        var result = await BuildService().CalculateBoundAsync(
            id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, 500, 5500, null, null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_not_found");
    }

    [Fact]
    public async Task CalculateBoundAsync_IdentityMismatch_Fails()
    {
        var binding = BindingWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:OTHER", "payer:1", Currency.Gbp, GrossMinor, 500, 5500, "pi_1", null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_mismatch");
    }

    [Fact]
    public async Task CalculateBoundAsync_BoundIntentDiffersFromSupplied_Fails()
    {
        var binding = BindingWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, 500, 5500, "pi_2", null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_intent_mismatch");
    }

    [Fact]
    public async Task CalculateBoundAsync_ExactIdentityAndIntentMatch_ReturnsBoundCommission()
    {
        var binding = BindingWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);
        var expected = calculator.Calculate(GrossMinor, Currency.Gbp, RateBasisPoints, VatRateBasisPoints);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor,
            expected.CommissionGrossMinor, expected.PayerTotalMinor, "pi_1", null);

        Assert.True(result.IsSuccess);
        Assert.Same(binding, result.Value.Binding);
        Assert.Same(configuration, result.Value.Configuration);
        Assert.Equal(expected, result.Value.Calculation);
    }

    [Fact]
    public async Task CalculateBoundAsync_CalculationDiffersFromExpected_ReturnsPricingChanged()
    {
        var binding = BindingWithConfiguration("booking:7", "payer:1", "pi_1");
        authorizationRepository
            .Setup(r => r.GetByIdAsync(binding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(binding);

        var result = await BuildService().CalculateBoundAsync(
            binding.Id, "booking:7", "payer:1", Currency.Gbp, GrossMinor, 1, 2, "pi_1", null);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "pricing_changed");
    }

    [Fact]
    public async Task FindBoundPaymentIntentAsync_ReturnsBoundIntent()
    {
        var binding = CommissionBindingEntity.Create(
            configurationId, "booking:7", "payer:1", timeProvider.GetUtcNow(), "pi_1");
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

    private CommissionBindingEntity BindingWithConfiguration(
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId)
    {
        var binding = CommissionBindingEntity.Create(
            configurationId, externalReference, payerReference, timeProvider.GetUtcNow(), stripePaymentIntentId);
        typeof(CommissionBindingEntity)
            .GetProperty(nameof(CommissionBindingEntity.CommissionConfiguration))!
            .SetValue(binding, configuration);
        return binding;
    }

    private CommissionService BuildService() =>
        new(
            configurationRepository.Object,
            authorizationRepository.Object,
            TestPaymentDbContext.Unopened(),
            unitOfWork.Object,
            calculator,
            Options.Create(new PlatformCommissionOptions
            {
                ConfigurationId = configurationId,
                Version = ConfigurationVersion,
                Currency = nameof(Currency.Gbp),
                RateBasisPoints = RateBasisPoints,
            }),
            Options.Create(new PlatformCommissionTaxOptions { VatRateBasisPoints = VatRateBasisPoints }),
            timeProvider);
}
