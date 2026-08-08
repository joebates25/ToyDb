using ToyDb;

Console.WriteLine("Hello, ToyDb!");

var database = Database.Initialize("file.db");


public readonly record struct PageNumber(int Value);
public readonly record struct DatabaseVersion(int Value);