using System.Text.Json;
using Microsoft.Playwright;

namespace Concertable.E2ETests.Support;

internal static class StripeResponseExtensions
{
    public static async Task EnsureStripeSuccessAsync(this IResponse response)
    {
        if (response.Ok) return;

        var message = "Stripe returned an error without a message.";
        try
        {
            using var document = JsonDocument.Parse(await response.TextAsync());
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var errorMessage))
                message = errorMessage.GetString() ?? message;
        }
        catch (JsonException) { }

        throw new InvalidOperationException($"Stripe confirmation failed ({response.Status}): {message}");
    }
}
