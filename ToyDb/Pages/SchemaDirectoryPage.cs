using System.Buffers.Binary;
using System.ComponentModel;

namespace ToyDb.Pages;

public class SchemaDirectoryPage(Memory<byte> data) : Page(data)
{
    private const int NumTablesOffset = 0;

    public int NumTables
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[NumTablesOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[NumTablesOffset..], value);
    }

    public void InsertSchemaPageNumber(int pageNum)
    {
        // Offset = 4 (Num Tables) + Num entries * size (4)
        BinaryPrimitives.WriteInt32LittleEndian(Data.Span[(4 + NumTables * 4)..], pageNum);
        NumTables++;
    }
}