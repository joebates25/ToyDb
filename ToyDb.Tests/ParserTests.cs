using ToyDb.AST;

namespace ToyDb.Tests;

public class ParserTests
{
    [Test]
    public void Parse_SelectAll_ReturnsSelectExpression()
    {
        var expression = new Parser().Parse("SELECT * FROM Users;");

        Assert.Multiple(() =>
        {
            Assert.That(expression.Columns, Is.EqualTo(new[] { "*" }));
            Assert.That(expression.TableName, Is.EqualTo("Users"));
            Assert.That(expression.WhereExpressions, Is.Null);
        });
    }

    [Test]
    public void Parse_SelectedColumns_ReturnsColumnsInOrder()
    {
        var expression = new Parser().Parse("select UserId, DisplayName from Users");

        Assert.Multiple(() =>
        {
            Assert.That(expression.Columns, Is.EqualTo(new[] { "UserId", "DisplayName" }));
            Assert.That(expression.TableName, Is.EqualTo("Users"));
        });
    }

    [Test]
    public void Parse_WherePredicates_ReturnsTypedValuesAndOperators()
    {
        const string sql = "select * from Users " +
                           "where Age > 21 and Name = 'O''Brien' and IsActive = TRUE and Score < 3000000000";

        var expression = new Parser().Parse(sql);

        Assert.That(expression.WhereExpressions, Is.EqualTo(new[]
        {
            WhereExpression.GreaterThan("Age", 21),
            WhereExpression.EqualTo("Name", "O'Brien"),
            WhereExpression.EqualTo("IsActive", true),
            WhereExpression.LessThan("Score", 3000000000L)
        }));
    }

    [TestCase("")]
    [TestCase("from Users")]
    [TestCase("select from Users")]
    [TestCase("select Name, from Users")]
    [TestCase("select Name Users")]
    [TestCase("select * from Users where Age")]
    [TestCase("select * from Users where Age >")]
    [TestCase("select * from Users where Age > 21 or Age < 50")]
    [TestCase("select * from Users;;")]
    public void Parse_InvalidSelect_ThrowsParserException(string sql)
    {
        Assert.Throws<ParserException>(() => new Parser().Parse(sql));
    }

    [Test]
    public void Parse_IntegerLargerThanInt64_ThrowsUsefulError()
    {
        var exception = Assert.Throws<ParserException>(() =>
            new Parser().Parse("select * from Users where Id = 999999999999999999999"));

        Assert.That(exception!.Message, Does.Contain("too large"));
    }
}
