using System.Runtime.CompilerServices;
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
    private const int SchemaDirectoryPageHeaderSize = sizeof(int);
    private const int DeletedTableValue = -1;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SchemaDirectoryPageHeader
    {
        internal int NumTables;
    }

    static SchemaDirectoryPage()
    {
        if (SchemaDirectoryPageHeaderSize != Unsafe.SizeOf<SchemaDirectoryPageHeader>())
            throw new InvalidOperationException("Schema directory page header size is invalid");

        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Invalid machine. little endian needed");
    }

    private ref SchemaDirectoryPageHeader Header =>
        ref MemoryMarshal.AsRef<SchemaDirectoryPageHeader>(Data.Span);

    private Span<int> SchemaPageNumberSpace =>
        MemoryMarshal.Cast<byte, int>(Data.Span[SchemaDirectoryPageHeaderSize..]);

    public int NumTables
    {
        get => Header.NumTables;
        set => Header.NumTables = value;
    }

    public int[] SchemaPageNumbers
    {
        get
        {
            var numTables = GetValidatedTableCount();
            return SchemaPageNumberSpace[..numTables].ToArray();
        }
    }

    public int[] NonDeletedSchemaPageNumbers => SchemaPageNumbers.Where(x => x != DeletedTableValue).ToArray();

    public void InsertSchemaDirectoryEntry(int pageNum)
    {
        var numTables = GetValidatedTableCount();
        if (numTables == SchemaPageNumberSpace.Length)
            throw new InvalidOperationException("The schema directory page is full.");

        SchemaPageNumberSpace[numTables] = pageNum;
        NumTables = numTables + 1;
    }

    public void ClearSchemaDirectoryEntry(int entrySlot)
    {
        var numTables = GetValidatedTableCount();
        if ((uint) entrySlot >= (uint) numTables)
            throw new ArgumentOutOfRangeException(nameof(entrySlot));

        SchemaPageNumberSpace[entrySlot] = DeletedTableValue;
    }

    private int GetValidatedTableCount()
    {
        var numTables = NumTables;
        if ((uint) numTables > (uint) SchemaPageNumberSpace.Length)
        {
            throw new InvalidDataException(
                $"Schema directory contains an invalid table count of {numTables}.");
        }

        return numTables;
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
