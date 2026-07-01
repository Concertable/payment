using Microsoft.Playwright;

namespace Concertable.E2ETests.Support;

public static class PageNavigationExtensions
{
    public static Task GotoSpaAsync(this IPage page, string url) =>
        page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
}
