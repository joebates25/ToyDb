using ToyDb.Pages;

using Microsoft.Extensions.Logging;

namespace ToyDb;

public class DatabaseManager(PageBufferManager pageBufferManager, ILoggerFactory loggerFactory)
{
    private ILogger Logger { get; } = loggerFactory.CreateLogger<DatabaseManager>();

    public async Task<PageLease<TPage>> LeaseNewPage<TPage>() where TPage : Page, IPageFactory<TPage>
    {
        using var headerPageLease = await pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0);
        headerPageLease.MarkDirty();
        var headerPage = headerPageLease.Page;
        var insertedPageNumber = ++headerPage.PageCount;
        Logger.LogDebug("Allocating page {PageNumber} of type {PageType}.", insertedPageNumber, typeof(TPage).Name);
        pageBufferManager.MarkPageDirty(insertedPageNumber);
        return pageBufferManager.AllocatePageLease<TPage>(insertedPageNumber);
    }
}

