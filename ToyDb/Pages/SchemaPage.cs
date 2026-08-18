using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ToyDb.Pages;

public class SchemaPage(Memory<byte> data) : Page(data), IPageFactory<SchemaPage>
{
    /* Some specs:
        Max Schema name length: 128
        Max num fields: ~30
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
    private const int SchemaPageHeaderSize = 140;
    private const int SchemaFieldSize = 130;

    [InlineArray(NameLengthBytes)]
    private struct SchemaString
    {
        private byte _element;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SchemaPageHeader
    {
        internal SchemaString Name;
        internal int FirstDataPageNumber;
        internal int LastDataPageNumber;
        internal int FieldCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SchemaFieldEntry
    {
        internal SchemaString Name;
        internal SchemaPageFieldType Type;
        internal byte Length;
    }

    static SchemaPage()
    {
        if (SchemaPageHeaderSize != Unsafe.SizeOf<SchemaPageHeader>())
            throw new InvalidOperationException("Schema page header size is invalid");

        if (SchemaFieldSize != Unsafe.SizeOf<SchemaFieldEntry>())
            throw new InvalidOperationException("Schema field size is invalid");
    }

    private ref SchemaPageHeader Header => ref MemoryMarshal.AsRef<SchemaPageHeader>(Data.Span);

    private Span<SchemaFieldEntry> FieldEntrySpace =>
        MemoryMarshal.Cast<byte, SchemaFieldEntry>(
            Data.Span[SchemaPageHeaderSize..]);

    public string Name
    {
        get => Encoding.UTF8.GetString(Header.Name).TrimEnd('\0');
        set
        {
            Header.Name = default;
            Encoding.UTF8.GetBytes(value).CopyTo(Header.Name);
        }
    }

    public int FirstDataPageNumber
    {
        get => Header.FirstDataPageNumber;
        set => Header.FirstDataPageNumber = value;
    }

    public int LastDataPageNumber
    {
        get => Header.LastDataPageNumber;
        set => Header.LastDataPageNumber = value;
    }

    public int FieldCount
    {
        get => Header.FieldCount;
        private set => Header.FieldCount = value;
    }

    public SchemaPageField[] Fields
    {
        get
        {
            var fieldCount = FieldCount;
            if (fieldCount < 0 || fieldCount > FieldEntrySpace.Length)
                throw new InvalidDataException("Invalid field count");

            var fields = new SchemaPageField[fieldCount];
            var offset = 0;

            for (var i = 0; i < fields.Length; i++)
            {
                ref var fieldEntry = ref FieldEntrySpace[i];

                var name = Encoding.UTF8.GetString(fieldEntry.Name).TrimEnd('\0');
                var type = fieldEntry.Type;
                var length = fieldEntry.Length;
                fields[i] = new SchemaPageField(name, type, length, offset);

                offset += length;
            }

            return fields;
        }
    }

    public void AddField(string name, SchemaPageFieldType type, byte length)
    {
        var fieldCount = FieldCount;

        if (fieldCount >= FieldEntrySpace.Length || fieldCount < 0)
            throw new ArgumentOutOfRangeException(nameof(fieldCount));

        ref var fieldSlot = ref FieldEntrySpace[fieldCount];
        fieldSlot = default;

        Encoding.UTF8.GetBytes(name).CopyTo(fieldSlot.Name);
        fieldSlot.Type   = type;
        fieldSlot.Length = length;

        FieldCount = fieldCount + 1;
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

public enum SchemaPageFieldType : byte
{
    Integer,
    Boolean,
    Long,
    String
}

public record SchemaPageField(string Name, SchemaPageFieldType Type, int Length, int Offset);
