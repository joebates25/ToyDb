using ToyDb;

var dbLocation = @"C:/users/josephbates/file.toydb";

Console.WriteLine("Hello, ToyDb!");

//
// var d = Database.Initialize(dbLocation);
// d.Dispose();
var database = await Database.OpenAsync(dbLocation);
Console.WriteLine(
    $"Version: {database.Header.Version} Page Directory= {database.Header.PageDirectoryPageNumber}, TableDirectory = {database.Header.TableDirectoryPageNumber}");
    
    