using ToyDb.Pages;

namespace ToyDb.Tests;

public class PageTests
{
    [Test]
    public void AccessingDataAfterPageIsDisposedThrows()
    {
        var data = new byte[Constants.PageSizeBytes];
        var page = DataPage.InitializePage(data);

        Assert.That(page.Data, Is.EqualTo(new Memory<byte>(data)));

        page.Dispose();

        Assert.That(() => page.Data, Throws.TypeOf<ObjectDisposedException>());
    }
}
