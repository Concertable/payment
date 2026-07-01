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

    public Task PayWithSavedCardAsync() => ConfirmButton.ClickAsync();

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        await CardTab.ClickAsync();
        await CardFrameElement.ScrollIntoViewIfNeededAsync();
        await FillCardAsync(cardNumber);
        await ConfirmButton.ClickAsync();
    }

    private async Task FillCardAsync(string cardNumber)
    {
        await FillFieldAsync(CardForm.Locator("[name='number']"), cardNumber);
        await FillFieldAsync(CardForm.Locator("[autocomplete='cc-exp']"), "1230");
        await FillFieldAsync(CardForm.Locator("[autocomplete='cc-csc']"), "123");
    }

    private static async Task FillFieldAsync(ILocator field, string value)
    {
        await field.ScrollIntoViewIfNeededAsync();
        await field.ClickAsync();
        await field.PressSequentiallyAsync(value, new() { Delay = 30 });
        await field.PressAsync("Tab");
    }
}
