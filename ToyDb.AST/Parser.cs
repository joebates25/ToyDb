using System.Globalization;

namespace ToyDb.AST;

/// <summary>
/// Parses the deliberately small SELECT grammar represented by the AST types.
/// </summary>
public sealed class Parser
{
    private IReadOnlyList<Token> _tokens = [];
    private int _tokenIndex;

    public SelectExpression Parse(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        _tokens = new Lexer().Tokenize(sql).ToArray();
        _tokenIndex = 0;

        ExpectKeyword("select");
        var columns = ParseColumns();
        ExpectKeyword("from");
        var tableName = Expect(TokenType.Ident, "a table name").Value;

        IReadOnlyList<WhereExpression>? whereExpressions = null;
        if (MatchKeyword("where"))
        {
            whereExpressions = ParseWhereExpressions();
        }

        Match(TokenType.SemiColon);
        if (!IsAtEnd)
        {
            throw Error($"Unexpected token {Describe(Current)} after the SELECT statement.");
        }

        return new SelectExpression(columns, tableName, whereExpressions);
    }

    private IReadOnlyList<string> ParseColumns()
    {
        if (MatchOperator("*"))
        {
            return ["*"];
        }

        var columns = new List<string>
        {
            Expect(TokenType.Ident, "a column name or '*'").Value
        };

        while (Match(TokenType.Comma))
        {
            columns.Add(Expect(TokenType.Ident, "a column name after ','").Value);
        }

        return columns;
    }

    private IReadOnlyList<WhereExpression> ParseWhereExpressions()
    {
        var expressions = new List<WhereExpression> { ParseWhereExpression() };
        while (MatchKeyword("and"))
        {
            expressions.Add(ParseWhereExpression());
        }

        return expressions;
    }

    private WhereExpression ParseWhereExpression()
    {
        var columnName = Expect(TokenType.Ident, "a column name in the WHERE clause").Value;
        var operatorToken = Expect(TokenType.Operator, "a comparison operator");
        var whereOperator = operatorToken.Value switch
        {
            ">" => WhereOperator.GreaterThan,
            "<" => WhereOperator.LessThan,
            "=" => WhereOperator.EqualTo,
            _ => throw Error($"'{operatorToken.Value}' is not a supported comparison operator.")
        };

        return new WhereExpression(columnName, whereOperator, ParseValue());
    }

    private object ParseValue()
    {
        if (IsAtEnd)
        {
            throw Error("Expected a value at the end of the WHERE clause.");
        }

        var token = Advance();
        if (token.TokenType == TokenType.String)
        {
            return token.Value;
        }

        if (token.TokenType == TokenType.Digit)
        {
            if (int.TryParse(token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            if (long.TryParse(token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }

            throw Error($"Integer literal '{token.Value}' is too large.", _tokenIndex - 1);
        }

        if (IsKeyword(token, "true"))
        {
            return true;
        }

        if (IsKeyword(token, "false"))
        {
            return false;
        }

        throw Error($"Expected an integer, string, or boolean value, but found {Describe(token)}.",
            _tokenIndex - 1);
    }

    private void ExpectKeyword(string keyword)
    {
        if (!MatchKeyword(keyword))
        {
            throw Error($"Expected keyword '{keyword.ToUpperInvariant()}', but found {DescribeCurrent()}.");
        }
    }

    private Token Expect(TokenType tokenType, string expectation)
    {
        if (!IsAtEnd && Current.TokenType == tokenType)
        {
            return Advance();
        }

        throw Error($"Expected {expectation}, but found {DescribeCurrent()}.");
    }

    private bool Match(TokenType tokenType)
    {
        if (IsAtEnd || Current.TokenType != tokenType)
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool MatchKeyword(string keyword)
    {
        if (IsAtEnd || !IsKeyword(Current, keyword))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool MatchOperator(string @operator)
    {
        if (IsAtEnd ||
            Current.TokenType != TokenType.Operator ||
            !StringComparer.Ordinal.Equals(Current.Value, @operator))
        {
            return false;
        }

        Advance();
        return true;
    }

    private static bool IsKeyword(Token token, string keyword) =>
        token.TokenType == TokenType.Keyword &&
        StringComparer.OrdinalIgnoreCase.Equals(token.Value, keyword);

    private Token Advance() => _tokens[_tokenIndex++];

    private Token Current => _tokens[_tokenIndex];

    private bool IsAtEnd => _tokenIndex >= _tokens.Count;

    private string DescribeCurrent() => IsAtEnd ? "the end of input" : Describe(Current);

    private static string Describe(Token token) =>
        string.IsNullOrEmpty(token.Value)
            ? $"{token.TokenType}"
            : $"'{token.Value}'";

    private ParserException Error(string message, int? tokenIndex = null) =>
        new(message, tokenIndex ?? _tokenIndex);
}

public sealed class ParserException(string message, int tokenIndex)
    : Exception($"{message} (token {tokenIndex})")
{
    public int TokenIndex { get; } = tokenIndex;
}
