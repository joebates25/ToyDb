using System.Buffers.Binary;
using System.ComponentModel;

namespace ToyDb.Pages;

// wip
public class PageDirectoryPage(Memory<byte> data) : Page(data)
{
    private const int OverflowPageNumberOffset = 0;
    // private const int PageDirectoryEntrySize = sizeof(int) + 
    
    // public int NumPageEntries
    // {
    //     get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[NumPageEntriesOffset..]);
    //     set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[NumPageEntriesOffset..], value);
    // }

    public int OverflowPageNumber
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[OverflowPageNumberOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[OverflowPageNumberOffset..], value);
    }

    public void InsertPagePageNumber(int pageNumber )
    {
        // Offset = 4 (Num Tables) + Num entries * size (4)
        // BinaryPrimitives.WriteInt32LittleEndian(Data.Span[(4 + NumPageEntries * 4)..], pageNumber);
    }
}