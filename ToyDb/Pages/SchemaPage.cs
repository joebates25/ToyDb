namespace ToyDb.Pages;

public class SchemaPage(Memory<byte> data) : Page(data)

{
    // default length 128 no matter what -- padded
    public string Name { get; set; }
    public int NumFields { get; private set; }
    public SchemaPageField[] Fields = [];

    public void AddField(string name, Type type, int length, int numFields)
    {
    }

    public void ClearFields()
    {
        NumFields = 0;
    }
}

public record SchemaPageField(string Name, SchemaPageFieldType Type, int Length, int Offset);

public enum SchemaPageFieldType
{
    Integer,
    Boolean,
    Long,
    String
}