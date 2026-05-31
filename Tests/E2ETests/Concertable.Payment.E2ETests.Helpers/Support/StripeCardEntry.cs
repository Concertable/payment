using Microsoft.Playwright;

namespace Concertable.E2ETests.Support;

public sealed class StripeCardEntry(IPageAccessor accessor)
{
    private IPage Page => accessor.Page;

    private IFrameLocator CardForm =>
        Page.FrameLocator("iframe[src*='elements-inner-accessory-target']");

    private ILocator CardTab => CardForm.GetByText("Card", new() { Exact = true });
    private ILocator ConfirmButton => Page.GetByTestId("confirm");

    public Task PayWithSavedCardAsync() => ConfirmButton.ClickAsync();

    public async Task PayWithNewCardAsync(string cardNumber)
    {
        await CardTab.ClickAsync();
        await ConfirmButton.ScrollIntoViewIfNeededAsync();
        await FillCardAsync(cardNumber);
        await ConfirmButton.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await ConfirmButton.ClickAsync();
    }

    private async Task FillCardAsync(string cardNumber)
    {
        var number = CardForm.Locator("[name='number']");
        var expiry = CardForm.Locator("[autocomplete='cc-exp']");
        var cvc = CardForm.Locator("[autocomplete='cc-csc']");

        await number.ClickAsync();
        await number.PressSequentiallyAsync(cardNumber, new() { Delay = 30 });
        await number.PressAsync("Tab");

        await expiry.ClickAsync();
        await expiry.PressSequentiallyAsync("1230", new() { Delay = 30 });
        await expiry.PressAsync("Tab");

        await cvc.ClickAsync();
        await cvc.PressSequentiallyAsync("123", new() { Delay = 30 });
        await cvc.PressAsync("Tab");
    }
}
