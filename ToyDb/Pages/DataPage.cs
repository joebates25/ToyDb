namespace ToyDb.Pages;

public class DataPage(Memory<byte> data) : Page(data), IPageFactory<DataPage>
{
    public static DataPage CreatePage(Memory<byte> data)
    {
        return new DataPage(data);
    }

    public static DataPage InitializePage(Memory<byte> data)
    {
        return new DataPage(data);
    }
}