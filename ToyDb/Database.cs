using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.Win32.SafeHandles;

namespace ToyDb;

public class Database
{
    static readonly int Version = 1;
    const string DbIdentifier = "Welcome to ToyDb!"; 

    public static Database Initialize(string filePath)
    {
        if (File.Exists(filePath))
        {
            throw new Exception("The file already exists. Try using Open()");
        }

        Span<byte> headerBytes = stackalloc byte[29];
        
        "Welcome to ToyDb!"u8.CopyTo(headerBytes);

        BinaryPrimitives.WriteInt32LittleEndian(headerBytes[17..], Version);
        BinaryPrimitives.WriteInt32LittleEndian(headerBytes[21..], 0);
        BinaryPrimitives.WriteInt32LittleEndian(headerBytes[25..], 0);

        using var safeHandle = File.OpenHandle(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

        RandomAccess.Write(safeHandle, headerBytes, 0);
        
        RandomAccess.FlushToDisk(safeHandle);
        
        return new Database();
    }

    public static Database Open(string filePath)
    {
        Console.WriteLine("Pretending to open a database");
        return new Database();
    }
}

public record DatabaseHeader
{
    public int Version { get; init; }
    public int PageDirectoryPageNumber { get; init; }
    public int TableDirectoryPageNumber { get; init; }
}