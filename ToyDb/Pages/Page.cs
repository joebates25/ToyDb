using ToyDb.Pages;

namespace ToyDb;

public class Page(Memory<byte> data)
{
    public int PageSize => Constants.PageSizeBytes;
    protected int PageType = 0;
    internal Memory<byte> Data = data;

    // problem: will need a new method here for every page
    
    public SchemaDirectoryPage AsSchemaDirectoryPage()
    {
        return new SchemaDirectoryPage(Data);
    }
    
    public SchemaPage AsSchemaPage()
    {
        return new SchemaPage(Data);
    }
}

public static class PageExtensions
{
    public static DatabaseHeaderPage AsDatabaseHeaderPage(this Page page)
    {
        return new DatabaseHeaderPage(page.Data);
    }
}