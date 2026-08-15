namespace ToyDb.Pages;

using System.Buffers.Binary;

public class DataPage(Memory<byte> data) : Page(data), IPageFactory<DataPage>
{
    /*
        Data page layout (4096 bytes):
        +-----------------------------------+ byte 0
        | SlotCount (4 bytes, LE)           |
        +-----------------------------------+ byte 4
        | FreeSpaceEnd (4 bytes, LE)        |
        +-----------------------------------+ byte 8
        | OverFlowPageNumber (4 bytes, LE)  |
        +-----------------------------------+ byte 12
        | Slot 0 (5 bytes)                  |
        |   InUse (1 byte)                  |
        |   OffsetStart (2 bytes, LE)       |
        |   Length (2 bytes, LE)            |
        +-----------------------------------+ byte 17
        | Slot 1 (5 bytes)                  |
        | ...                               | slots grow downward
        +-----------------------------------+ byte 12 + (SlotCount * 5)
        |                                   |
        | Free space                        |
        |                                   |
        +-----------------------------------+ byte FreeSpaceEnd
        | Record data                       |
        | ...                               | records grow upward
        +-----------------------------------+ byte 4096
    */
    private const int SlotCountOffset = 0;
    private const int FreeSpaceEndOffset = SlotCountOffset + sizeof(int);
    private const int OverFlowPageNumberOffset = FreeSpaceEndOffset + sizeof(int);
    private const int HeaderSize = OverFlowPageNumberOffset + sizeof(int);

    public int SlotCount
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[SlotCountOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[SlotCountOffset..], value);
    }

    public int FreeSpaceEnd
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[FreeSpaceEndOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[FreeSpaceEndOffset..], value);
    }

    public int OverFlowPageNumber
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.Span[OverFlowPageNumberOffset..]);
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.Span[OverFlowPageNumberOffset..], value);
    }

    public IEnumerable<Slot> EnumerateSlots()
    {
        var slotCount = SlotCount;
        var maximumSlotCount = (Data.Length - HeaderSize) / SlotSize;
        if ((uint) slotCount > (uint) maximumSlotCount)
        {
            throw new InvalidDataException(
                $"Page contains an invalid slot count of {slotCount}.");
        }

        for (var index = 0; index < slotCount; index++)
        {
            yield return this[index];
        }
    }

    public int FreeSpaceSize => FreeSpaceEnd - HeaderSize - (SlotSize * SlotCount);

    public Slot this[int index]
    {
        get
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var slotOffset = HeaderSize + SlotSize * index;
            var slotData = Data.Span.Slice(slotOffset, SlotSize);

            return new Slot(
                InUse: slotData[0] != 0,
                OffsetStart: BinaryPrimitives.ReadUInt16LittleEndian(slotData[sizeof(bool)..]),
                Length: BinaryPrimitives.ReadUInt16LittleEndian(
                    slotData[(sizeof(bool) + sizeof(ushort))..]));
        }
    }

    public Slot InsertData(ReadOnlyMemory<byte> data)
    {
        // Confirm enough space
        var requiredSpace = SlotSize + data.Length;
        if (requiredSpace > FreeSpaceSize)
        {
            throw new InvalidOperationException(
                $"Data requires {requiredSpace} bytes, but the page only has {FreeSpaceSize} bytes available.");
        }

        // calculate offsets 
        var slotOffset = HeaderSize + SlotSize * SlotCount;
        var dataOffset = FreeSpaceEnd - data.Length;

        // Create slot based on data 
        var slot = new Slot(
            InUse: true,
            OffsetStart: checked((ushort) dataOffset),
            Length: checked((ushort) data.Length));

        // write slot
        var slotSpan = Data.Span.Slice(slotOffset, SlotSize);
        slotSpan[0] = slot.InUse ? (byte) 1 : (byte) 0;
        BinaryPrimitives.WriteUInt16LittleEndian(slotSpan[sizeof(bool)..], slot.OffsetStart);
        BinaryPrimitives.WriteUInt16LittleEndian(
            slotSpan[(sizeof(bool) + sizeof(ushort))..], slot.Length);

        // write data 
        data.Span.CopyTo(Data.Span.Slice(dataOffset, data.Length));
        FreeSpaceEnd = dataOffset;
        SlotCount++;

        // return slot 
        return slot;
    }

    public void FreeSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        var slotOffset = HeaderSize + SlotSize * slotIndex;
        Data.Span[slotOffset] = 0;
    }

    public static DataPage CreatePage(Memory<byte> data)
    {
        return new DataPage(data);
    }

    public static DataPage InitializePage(Memory<byte> data)
    {
        var page = new DataPage(data)
        {
            SlotCount          = 0,
            FreeSpaceEnd       = data.Length,
            OverFlowPageNumber = -1
        };

        return page;
    }

    public record Slot(bool InUse, ushort OffsetStart, ushort Length);

    public const int SlotSize = sizeof(bool) + sizeof(ushort) + sizeof(ushort);
}
