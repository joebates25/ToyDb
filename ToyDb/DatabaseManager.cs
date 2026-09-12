using ToyDb.Pages;

namespace ToyDb;

public class DatabaseManager(PageBufferManager pageBufferManager)
{
    public async Task<PageLease<TPage>> LeaseNewPage<TPage>() where TPage : Page, IPageFactory<TPage>
    {
        using var headerPageLease = await pageBufferManager.LeasePageAsync<DatabaseHeaderPage>(0);
        headerPageLease.MarkDirty();
        var headerPage = headerPageLease.Page;
        var insertedPageNumber = ++headerPage.PageCount;
        pageBufferManager.MarkPageDirty(insertedPageNumber);
        return pageBufferManager.AllocatePageLease<TPage>(insertedPageNumber);
    }
}

