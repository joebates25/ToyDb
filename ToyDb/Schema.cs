namespace ToyDb;

public class Schema(string name)
{
    public string Name { get; set; } = name;
    public List<Field> Fields { get; set; } = [];

    // todo: should not need length except for strings 
    public Schema AddField(string name, SchemaFieldType type, byte length)
    {
        if (length == 0) throw new ArgumentNullException(nameof(length));

        Fields.Add(new Field(name, type, Fields.Sum(x => x.Length), length));
        return this;
    }
}

public class Field(string name, SchemaFieldType type, int offset, byte length)
{
    public string Name { get; set; } = name;
    public SchemaFieldType Type { get; set; } = type;
    public int Offset { get; set; } = offset;
    public byte Length { get; set; } = length;
}

public enum SchemaFieldType
{
    String,
    Integer,
    Long,
    Boolean,
}