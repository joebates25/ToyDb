using System.Buffers.Binary;
using System.Text;

namespace ToyDb.Pages;

public class SchemaPage(Memory<byte> data) : Page(data), IPageFactory<SchemaPage>
{
    /* Some specs:
        Max Schema name length: 128
        Max num fields: ~31
        Max field name length: 128
        Max field size: 255

        Schema page layout (4096 bytes):
        +---------------------------+ byte 0
        | Name (128 bytes)          |
        +---------------------------+ byte 128
        | FirstDataPageNumber       |
        | (4 bytes, LE)             |
        +---------------------------+ byte 132
        | LastDataPageNumber        |
        | (4 bytes, LE)             |
        +---------------------------+ byte 136
        | FieldCount (4 bytes, LE)  |
        +---------------------------+ byte 140
        | Fields (130 bytes each)   |
        |   +---------------------+ |
        |   | Name (128 bytes)    | |
        |   +---------------------+ |
        |   | Type (1 byte)       | |
        |   +---------------------+ |
        |   | Length (1 byte)     | |
        |   +---------------------+ |
        +---------------------------+
    */
    private const int NameLengthBytes = 128;
    private const int FirstDataPageNumberOffset = NameLengthBytes;
    private const int LastDataPageNumberOffset = FirstDataPageNumberOffset + sizeof(int);
    private const int FieldCountOffset = LastDataPageNumberOffset + sizeof(int);
    private const int FieldsOffset = FieldCountOffset + sizeof(int);

    // default length 128 no matter what -- padded
    public string Name
    {
        get => Encoding.UTF8.GetString(Data.Span[..NameLengthBytes]).TrimEnd('\0');
        set
        {
            Data.Span[..NameLengthBytes].Clear();
            Encoding.UTF8.GetBytes(value).CopyTo(Data.Span[..NameLengthBytes]);
        }
    }

    public int FirstDataPageNumber
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[FirstDataPageNumberOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[FirstDataPageNumberOffset..], value);
    }

    public int LastDataPageNumber
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[LastDataPageNumberOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[LastDataPageNumberOffset..], value);
    }

    public int FieldCount
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[FieldCountOffset..]);
        private set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[FieldCountOffset..], value);
    }

    public SchemaPageField[] Fields
    {
        get
        {
            var fields = new SchemaPageField[FieldCount];
            var offset = 0;

            for (var i = 0; i < fields.Length; i++)
            {
                var fieldSlot = Data.Span.Slice(FieldsOffset + i * FieldSizeBytes, FieldSizeBytes);
                var name = Encoding.UTF8.GetString(fieldSlot[..NameLengthBytes]).TrimEnd('\0');
                var type = (SchemaPageFieldType)fieldSlot[NameLengthBytes];
                var length = fieldSlot[NameLengthBytes + 1];

                fields[i] = new SchemaPageField(name, type, length, offset);
                offset += length;
            }

            return fields;
        }
    }

    // Field size = 128 (name) + 1 (type) + 1 (length) = 130
    private const int FieldSizeBytes = 128 + 1 + 1;

    public void AddField(string name, SchemaPageFieldType type, byte length)
    {
        if (FieldCount == 31) throw new ArgumentOutOfRangeException(nameof(FieldCount));

        var fieldSlot = Data.Span.Slice(FieldsOffset + FieldCount * FieldSizeBytes, FieldSizeBytes);
        fieldSlot.Clear();
        Encoding.UTF8.GetBytes(name).CopyTo(fieldSlot);
        fieldSlot[NameLengthBytes] = (byte)type;
        fieldSlot[NameLengthBytes + 1] = length;

        FieldCount++;
    }

    public static SchemaPage CreatePage(Memory<byte> data)
    {
        return new SchemaPage(data);
    }

    public static SchemaPage InitializePage(Memory<byte> data)
    {
        return new SchemaPage(data);
    }
}

public record SchemaPageField(string Name, SchemaPageFieldType Type, int Length, int Offset);

public enum SchemaPageFieldType : byte
{
    Integer,
    Boolean,
    Long,
    String
}
