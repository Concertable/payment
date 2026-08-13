using Microsoft.Playwright;

namespace Concertable.Payment.E2ETests.Helpers.Ui;

public static class PageNavigationExtensions
{
    public static Task GotoSpaAsync(this IPage page, string url) =>
        page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

    /// <summary>
    /// Awaits a client-side (SPA) navigation to <paramref name="urlPattern"/>, resolving on URL commit
    /// rather than the <see cref="WaitUntilState.Load"/> state. Pages like checkout hold connections open
    /// (Stripe iframes, SignalR), so a load-state wait can hang the full timeout even though the route
    /// already changed — assert the committed URL and let the next step's element waits gate readiness.
    /// </summary>
    public static Task WaitForSpaUrlAsync(this IPage page, string urlPattern) =>
        page.WaitForURLAsync(urlPattern, new PageWaitForURLOptions { WaitUntil = WaitUntilState.Commit });
}
