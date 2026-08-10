using ToyDb.Pages;

namespace ToyDb;

public class Page(Memory<byte> data)
{
    public int PageSize => Constants.PageSizeBytes;
    protected int PageType = 0;
    protected Memory<byte> Data = data;

    // problem: will need a new method here for every page
    public DatabaseHeaderPage AsDatabaseHeaderPage()
    {
        return new DatabaseHeaderPage(Data);
    }
    
    public SchemaDirectoryPage AsSchemaDirectoryPage()
    {
        return new SchemaDirectoryPage(Data);
    }
}