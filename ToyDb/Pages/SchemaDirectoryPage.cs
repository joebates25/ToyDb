using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ToyDb.Pages;

public class SchemaDirectoryPage(Memory<byte> data) : Page(data), IPageFactory<SchemaDirectoryPage>
{
    /*
        Schema directory page layout (4096 bytes):
        +----------------------------------+ byte 0
        | NumTables (4 bytes, LE)          |
        +----------------------------------+ byte 4
        | SchemaPageNumbers (4 bytes each) |
        |   +----------------------------+ |
        |   | Page number (4 bytes, LE)  | |
        |   +----------------------------+ |
        |   | ...                        | |
        |   +----------------------------+ |
        +----------------------------------+

        A page number of -1 marks a deleted table.
    */
    private const int NumTablesOffset = 0;

    private const int DeletedTableValue = -1;

    public int NumTables
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[..], value);
    }

    public int[] SchemaPageNumbers =>
        MemoryMarshal.Cast<byte, int>(Data.Span[(NumTablesOffset + 4)..(NumTables * Constants.PageNumberSize)])
            .ToArray();

    public int[] NonDeletedSchemaPageNumbers => SchemaPageNumbers.Where(x => x != DeletedTableValue).ToArray();

    public void InsertSchemaDirectoryEntry(int pageNum)
    {
        BinaryPrimitives.WriteInt32LittleEndian(
            Data.Span[((NumTablesOffset + 4) + NumTables * Constants.PageNumberSize)..], pageNum);
        NumTables++;
    }

    public void ClearSchemaDirectoryEntry(int entrySlot)
    {
        BinaryPrimitives.WriteInt32LittleEndian(
            Data.Span[((NumTablesOffset + 4) + entrySlot * Constants.PageNumberSize)..], DeletedTableValue);
    }

    public static SchemaDirectoryPage CreatePage(Memory<byte> data)
    {
        return new SchemaDirectoryPage(data);
    }

    public static SchemaDirectoryPage InitializePage(Memory<byte> data)
    {
        return new SchemaDirectoryPage(data);
    }
}