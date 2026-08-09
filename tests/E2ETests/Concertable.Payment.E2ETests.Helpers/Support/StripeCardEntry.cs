using System.Text.Json;
using Microsoft.Playwright;

namespace Concertable.E2ETests.Support;

public sealed class StripeCardEntry(IPageAccessor accessor)
{
    private IPage Page => accessor.Page;

    private const string CardFrameSelector = "iframe[src*='elements-inner-accessory-target']";

    private IFrameLocator CardForm => Page.FrameLocator(CardFrameSelector);
    private ILocator CardFrameElement => Page.Locator(CardFrameSelector);

    private ILocator CardTab => CardForm.GetByText("Card", new() { Exact = true });
    private ILocator ConfirmButton => Page.GetByTestId("confirm");

    public Task PayWithSavedCardAsync() => ConfirmAsync();

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        await CardFrameElement.ScrollIntoViewIfNeededAsync();
        await CardTab.ClickAsync();
        await FillCardAsync(cardNumber);
        await ConfirmAsync();
    }

    private async Task FillCardAsync(string cardNumber)
    {
        await FillFieldAsync(CardForm.Locator("[name='number']"), cardNumber);
        await FillFieldAsync(CardForm.Locator("[autocomplete='cc-exp']"), "1230");
        await FillFieldAsync(CardForm.Locator("[autocomplete='cc-csc']"), "123");
    }

    private async Task ConfirmAsync()
    {
        var confirmationResponse = Page.WaitForResponseAsync(response =>
            response.Request.Method == "POST" &&
            response.Url.StartsWith("https://api.stripe.com/v1/", StringComparison.OrdinalIgnoreCase) &&
            response.Url.EndsWith("/confirm", StringComparison.OrdinalIgnoreCase));

        await ConfirmButton.ClickAsync();
        var response = await confirmationResponse;

        if (response.Ok) return;

        var body = await response.TextAsync();
        var message = "Stripe returned an error without a message.";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var errorMessage))
                message = errorMessage.GetString() ?? message;
        }
        catch (JsonException)
        {
        }

        throw new InvalidOperationException($"Stripe confirmation failed ({response.Status}): {message}");
    }

    private static async Task FillFieldAsync(ILocator field, string value)
    {
        await field.ScrollIntoViewIfNeededAsync();
        await field.ClickAsync();
        await field.PressSequentiallyAsync(value, new() { Delay = 30 });
        await field.PressAsync("Tab");
    }
}
