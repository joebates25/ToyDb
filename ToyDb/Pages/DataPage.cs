namespace ToyDb.Pages;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        | cell data                         |
        | ...                               | records grow upward
        +-----------------------------------+ byte 4096
    */
    private const int DataPageHeaderSize = 12;
    private const int DataPageSlotEntrySize = 5;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DataPageHeader
    {
        internal int SlotCount;
        internal int FreeSpaceEnd;
        internal int OverFlowPageNumber;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DataPageSlot
    {
        internal byte InUse;
        internal ushort OffsetStart;
        internal ushort Length;
    }

    static DataPage()
    {
        if (DataPageHeaderSize != Unsafe.SizeOf<DataPageHeader>())
            throw new InvalidOperationException("Data page header size is invalid");

        if (DataPageSlotEntrySize != Unsafe.SizeOf<DataPageSlot>())
            throw new InvalidOperationException("Data page slot entry size is invalid");
    }

    private ref DataPageHeader Header => ref MemoryMarshal.AsRef<DataPageHeader>(Data.Span);

    private Span<DataPageSlot> Slots =>
        MemoryMarshal.Cast<byte, DataPageSlot>(Data.Span[DataPageHeaderSize..]);

    public int SlotCount
    {
        get => Header.SlotCount;
        set => Header.SlotCount = value;
    }

    public int FreeSpaceEnd
    {
        get => Header.FreeSpaceEnd;
        set => Header.FreeSpaceEnd = value;
    }

    public int OverFlowPageNumber
    {
        get => Header.OverFlowPageNumber;
        set => Header.OverFlowPageNumber = value;
    }

    public IEnumerable<Slot> EnumerateSlots()
    {
        var slotCount = SlotCount;
        if ((uint) slotCount > (uint) Slots.Length)
        {
            throw new InvalidDataException(
                $"Page contains an invalid slot count of {slotCount}.");
        }

        for (var index = 0; index < slotCount; index++)
        {
            yield return this[index];
        }
    }

    public int FreeSpaceSize => FreeSpaceEnd - DataPageHeaderSize - (SlotSize * SlotCount);

    public Slot this[int index]
    {
        get
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            ref var slotEntry = ref Slots[index];

            return new Slot(
                InUse: slotEntry.InUse != 0,
                OffsetStart: slotEntry.OffsetStart,
                Length: slotEntry.Length);
        }
        internal set
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Slots[index] = new DataPageSlot
            {
                InUse       = (byte) (value.InUse ? 1 : 0),
                OffsetStart = value.OffsetStart,
                Length      = value.Length
            };
        }
    }

    public Slot InsertCell(ReadOnlyMemory<byte> cellData)
    {
        // Confirm enough space
        var requiredSpace = SlotSize + cellData.Length;
        if (requiredSpace > FreeSpaceSize)
        {
            throw new InvalidOperationException(
                $"Data requires {requiredSpace} bytes, but the page only has {FreeSpaceSize} bytes available.");
        }

        // calculate offsets
        var slotCount = SlotCount;
        var dataOffset = FreeSpaceEnd - cellData.Length;

        // Create slot based on data 
        var slot = new Slot(
            InUse: true,
            OffsetStart: checked((ushort) dataOffset),
            Length: checked((ushort) cellData.Length));

        // write slot
        ref var slotEntry = ref Slots[slotCount];
        slotEntry             = default;
        slotEntry.InUse       = slot.InUse ? (byte) 1 : (byte) 0;
        slotEntry.OffsetStart = slot.OffsetStart;
        slotEntry.Length      = slot.Length;

        // write data 
        cellData.Span.CopyTo(Data.Span.Slice(dataOffset, cellData.Length));
        FreeSpaceEnd = dataOffset;
        SlotCount    = slotCount + 1;

        // return slot 
        return slot;
    }

    public void FreeSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        Slots[slotIndex].InUse = 0;
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

    public const int SlotSize = DataPageSlotEntrySize;
}