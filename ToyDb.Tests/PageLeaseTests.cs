using ToyDb.Pages;

namespace ToyDb.Tests;

public class PageLeaseTests
{
    [Test]
    public void AccessingPageAfterLeaseIsDisposedThrows()
    {
        using var page = DataPage.InitializePage(new byte[Constants.PageSizeBytes]);
        var lease = new PageLease<DataPage>(page, _ => { }, 0);

        Assert.That(lease.Page, Is.SameAs(page));

        lease.Dispose();

        Assert.That(() => lease.Page, Throws.TypeOf<ObjectDisposedException>());
    }
}
