using ToyDb.AST;

namespace ToyDb.Tests;

public class LexerTests
{
    [Test]
    public void Tokenize_SelectAll_ReturnsExpectedTokens()
    {
        var tokens = new Lexer().Tokenize("SELECT * FROM Users;").ToArray();

        Assert.That(tokens, Is.EqualTo(new[]
        {
            new Token(TokenType.Keyword, "SELECT"),
            new Token(TokenType.Operator, "*"),
            new Token(TokenType.Keyword, "FROM"),
            new Token(TokenType.Ident, "Users"),
            new Token(TokenType.SemiColon)
        }));
    }

    [Test]
    public void Tokenize_ColumnsAndWherePredicates_ReturnsExpectedTokens()
    {
        const string sql = "select user_id, name from users where age > 21 and name = 'O''Brien'";

        var tokens = new Lexer().Tokenize(sql).ToArray();

        Assert.That(tokens, Is.EqualTo(new[]
        {
            new Token(TokenType.Keyword, "select"),
            new Token(TokenType.Ident, "user_id"),
            new Token(TokenType.Comma),
            new Token(TokenType.Ident, "name"),
            new Token(TokenType.Keyword, "from"),
            new Token(TokenType.Ident, "users"),
            new Token(TokenType.Keyword, "where"),
            new Token(TokenType.Ident, "age"),
            new Token(TokenType.Operator, ">"),
            new Token(TokenType.Digit, "21"),
            new Token(TokenType.Keyword, "and"),
            new Token(TokenType.Ident, "name"),
            new Token(TokenType.Operator, "="),
            new Token(TokenType.String, "O'Brien")
        }));
    }

    [Test]
    public void Tokenize_KeywordsAreCaseInsensitive_AndIdentifiersPreserveTheirCase()
    {
        var tokens = new Lexer().Tokenize("SeLeCt Enabled FrOm Accounts WhErE Enabled = TrUe").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(tokens[0], Is.EqualTo(new Token(TokenType.Keyword, "SeLeCt")));
            Assert.That(tokens[1], Is.EqualTo(new Token(TokenType.Ident, "Enabled")));
            Assert.That(tokens[2], Is.EqualTo(new Token(TokenType.Keyword, "FrOm")));
            Assert.That(tokens[3], Is.EqualTo(new Token(TokenType.Ident, "Accounts")));
            Assert.That(tokens[4], Is.EqualTo(new Token(TokenType.Keyword, "WhErE")));
            Assert.That(tokens[5], Is.EqualTo(new Token(TokenType.Ident, "Enabled")));
            Assert.That(tokens[7], Is.EqualTo(new Token(TokenType.Keyword, "TrUe")));
        });
    }

    [Test]
    public void Tokenize_SqlCommentAtEndOfInput_IsIgnored()
    {
        var tokens = new Lexer().Tokenize("select * from users -- no newline").ToArray();

        Assert.That(tokens, Has.Length.EqualTo(4));
    }

    [Test]
    public void Tokenize_UnterminatedString_ReportsItsPosition()
    {
        var exception = Assert.Throws<LexerException>(() =>
            new Lexer().Tokenize("select * from users where name = 'Ada").ToArray());

        Assert.That(exception!.Position, Is.EqualTo(33));
        Assert.That(exception.Message, Does.Contain("Unterminated string literal"));
    }

    [Test]
    public void Tokenize_UnsupportedSyntax_ReportsTheCharacterAndPosition()
    {
        var exception = Assert.Throws<LexerException>(() =>
            new Lexer().Tokenize("select (name) from users").ToArray());

        Assert.That(exception!.Position, Is.EqualTo(7));
        Assert.That(exception.Message, Does.Contain("'('"));
    }
}
