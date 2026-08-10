using Microsoft.Playwright;

namespace Concertable.E2ETests.Support;

public sealed class StripeCardEntry(IPageAccessor accessor)
{
    private IPage Page => accessor.Page;

    private const string CardFrameSelector = "iframe[src*='elements-inner-accessory-target']";

    private IFrameLocator CardForm => Page.FrameLocator(CardFrameSelector);

    private ILocator CardTab => CardForm.GetByRole(AriaRole.Tab, new() { Name = "Card", Exact = true });
    private ILocator CardNumber => CardForm.Locator("[name='number']");
    private ILocator ConfirmButton => Page.GetByTestId("confirm");

    public Task PayWithSavedCardAsync() => ConfirmAsync();

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        await SelectCardAsync();
        await FillCardAsync(cardNumber);
        await ConfirmAsync();
    }

    private async Task SelectCardAsync()
    {
        if (await CardTab.GetAttributeAsync("aria-selected") != "true")
            await CardTab.ClickAsync();

        await Assertions.Expect(CardTab).ToHaveAttributeAsync("aria-selected", "true");
    }

    private async Task FillCardAsync(string cardNumber)
    {
        await FillFieldAsync(CardNumber, cardNumber);
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
        await confirmationResponse;
    }

    private static async Task FillFieldAsync(ILocator field, string value)
    {
        await field.ScrollIntoViewIfNeededAsync();
        await field.ClickAsync();
        await field.PressSequentiallyAsync(value, new() { Delay = 30 });
        await field.PressAsync("Tab");
    }
}
