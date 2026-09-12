namespace ToyDb.AST;

public record SelectExpression(
    IReadOnlyList<string> Columns,
    string TableName,
    IReadOnlyList<WhereExpression>? WhereExpressions = null) : IExpression
{
    public static SelectExpression All(string tableName, IReadOnlyList<WhereExpression>? whereExpressions = null) =>
        new (
            ["*"],
            tableName, whereExpressions);
}

public record WhereExpression(
    string ColumnName,
    WhereOperator Operator,
    object Value)
{
    public static WhereExpression GreaterThan(string columnName, object value) =>
        new WhereExpression(columnName, WhereOperator.GreaterThan, value);

    public static WhereExpression LessThan(string columnName, object value) =>
        new WhereExpression(columnName, WhereOperator.LessThan, value);

    public static WhereExpression EqualTo(string columnName, object value) =>
        new WhereExpression(columnName, WhereOperator.EqualTo, value);
}

public record WhereOperator(string Operator)
{
    public static WhereOperator GreaterThan => new(">");
    public static WhereOperator LessThan => new("<");
    public static WhereOperator EqualTo => new("=");
}

public interface IExpression
{
}

static class Foo
{
    static void bar()
    {
        // Select * from Table
        var selectAll = SelectExpression.All("Table");

        // Select Column1, Column2 from Table
        var orSelectColumns = new SelectExpression(
            ["Column1", "Column2"],
            "Table");

        // Select Column1, Column2 from Table where Column1 > 5
        var orColumnsWithWhere = new SelectExpression(
            ["Column1", "Column2"],
            "Table",
            [WhereExpression.GreaterThan("Column1", 5)]);

        // Select * from Table where Column1 > 5
        var orAllWithWhere = SelectExpression.All(
            "Table",
            [WhereExpression.GreaterThan("Column1", 5)]);
    }
}