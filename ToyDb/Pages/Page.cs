using ToyDb.Pages;

namespace ToyDb;

public abstract class Page(Memory<byte> data) 
{
    public int PageSize => Constants.PageSizeBytes;
    protected int PageType = 0;
    internal Memory<byte> Data = data;
}

public interface IPageFactory<TPage> where TPage : Page
{
    static abstract TPage CreatePage(Memory<byte> data);
    
    static abstract TPage InitializePage(Memory<byte> data);
}