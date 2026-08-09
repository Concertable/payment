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

    public async Task PayWithSavedCardAsync()
    {
        var response = await ConfirmAsync();
        await response.EnsureStripeSuccessAsync();
    }

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        var response = await SubmitNewCardAsync(cardNumber);
        await response.EnsureStripeSuccessAsync();
    }

    public Task PayWithDeclinedCardAsync(string cardNumber) =>
        SubmitNewCardAsync(cardNumber);

    private async Task<IResponse> SubmitNewCardAsync(string cardNumber)
    {
        await CardFrameElement.ScrollIntoViewIfNeededAsync();
        await CardTab.ClickAsync();
        await FillCardAsync(cardNumber);
        return await ConfirmAsync();
    }

    private async Task FillCardAsync(string cardNumber)
    {
        await FillFieldAsync(CardForm.Locator("[name='number']"), cardNumber);
        await FillFieldAsync(CardForm.Locator("[autocomplete='cc-exp']"), "1230");
        await FillFieldAsync(CardForm.Locator("[autocomplete='cc-csc']"), "123");
    }

    private async Task<IResponse> ConfirmAsync()
    {
        var confirmationResponse = Page.WaitForResponseAsync(response =>
            response.Request.Method == "POST" &&
            response.Url.StartsWith("https://api.stripe.com/v1/", StringComparison.OrdinalIgnoreCase) &&
            response.Url.EndsWith("/confirm", StringComparison.OrdinalIgnoreCase));

        await ConfirmButton.ClickAsync();
        return await confirmationResponse;
    }

    private static async Task FillFieldAsync(ILocator field, string value)
    {
        await field.ScrollIntoViewIfNeededAsync();
        await field.ClickAsync();
        await field.PressSequentiallyAsync(value, new() { Delay = 30 });
        await field.PressAsync("Tab");
    }
}
