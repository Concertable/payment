using Microsoft.Playwright;

namespace Concertable.E2ETests.Support;

public sealed class StripePayment(IPageAccessor accessor, StripeCardEntry cardEntry) : IStripePayment
{
    private IPage Page => accessor.Page;

    public Task PayWithSavedCardAsync() => cardEntry.PayWithSavedCardAsync();
    public Task PayWithNewCardAsync(string cardNumber) => cardEntry.PayWithNewCardAsync(cardNumber);

    public async Task CompleteChallengeIfRequiredAsync()
    {
        var outer = Page.Locator("iframe[src*='three-ds-2-challenge']");
        try
        {
            await outer.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 5_000 });
        }
        catch (Exception)
        {
            return;
        }

        var challengeFrame = outer.ContentFrame.Locator("#challengeFrame");
        await challengeFrame.WaitForAsync(new() { State = WaitForSelectorState.Attached });

        var button = challengeFrame.ContentFrame.Locator("#test-source-authorize-3ds");
        await button.WaitForAsync(new() { State = WaitForSelectorState.Attached });

        await button.ClickAsync();
        await outer.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    public async Task CompleteChallengeAsync()
    {
        var outer = Page.Locator("iframe[src*='three-ds-2-challenge']");
        await outer.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 30_000 });

        var challengeFrame = outer.ContentFrame.Locator("#challengeFrame");
        await challengeFrame.WaitForAsync(new() { State = WaitForSelectorState.Attached });

        var button = challengeFrame.ContentFrame.Locator("#test-source-authorize-3ds");
        await button.WaitForAsync(new() { State = WaitForSelectorState.Attached });

        await button.ClickAsync();
        await outer.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    public async Task FailChallengeAsync()
    {
        var outer = Page.Locator("iframe[src*='three-ds-2-challenge']");
        await outer.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 30_000 });

        var challengeFrame = outer.ContentFrame.Locator("#challengeFrame");
        await challengeFrame.WaitForAsync(new() { State = WaitForSelectorState.Attached });

        var button = challengeFrame.ContentFrame.Locator("#test-source-fail-3ds");
        await button.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await button.ClickAsync();
        await outer.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }
}
