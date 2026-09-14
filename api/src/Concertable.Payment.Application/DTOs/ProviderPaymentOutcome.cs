namespace Concertable.Payment.Application.DTOs;

internal sealed record ProviderPaymentOutcome(
    string ProviderTransactionId,
    bool RequiresAction = false,
    string? ClientSecret = null);

internal sealed record ProviderTransfer(string ProviderTransferId);

internal sealed record ProviderRefund(string ProviderRefundId);
