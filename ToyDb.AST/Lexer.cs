using System.Text;

namespace ToyDb.AST;

/// <summary>
/// Tokenizes the small SQL subset represented by <see cref="SelectExpression"/>.
/// </summary>
public sealed class Lexer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select",
        "from",
        "where",
        "and",
        "true",
        "false"
    };

    private string _input = string.Empty;
    private int _inputIndex;
    private Token? _peekedToken;

    public IEnumerable<Token> Tokenize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        _input = input;
        _inputIndex = 0;
        _peekedToken = null;

        Token? token;
        while ((token = NextToken()) is not null)
        {
            yield return token;
        }
    }

    /// <summary>
    /// Returns the next token without consuming it. This is useful to a parser
    /// consuming the sequence returned by <see cref="Tokenize"/>.
    /// </summary>
    public Token? PeekNextToken()
    {
        return _peekedToken ??= ReadNextToken();
    }

    private Token? NextToken()
    {
        if (_peekedToken is null)
        {
            return ReadNextToken();
        }

        var token = _peekedToken;
        _peekedToken = null;
        return token;
    }

    private Token? ReadNextToken()
    {
        SkipTrivia();
        if (IsAtEnd)
        {
            return null;
        }

        var tokenStart = _inputIndex;
        var current = Advance();

        return current switch
        {
            '*' => new Token(TokenType.Operator, "*"),
            ',' => new Token(TokenType.Comma),
            ';' => new Token(TokenType.SemiColon),
            '<' or '>' or '=' => new Token(TokenType.Operator, current.ToString()),
            '\'' => ReadString(tokenStart),
            _ when char.IsDigit(current) => ReadInteger(current),
            _ when IsIdentifierStart(current) => ReadWord(current),
            _ => throw UnexpectedCharacter(current, tokenStart)
        };
    }

    private void SkipTrivia()
    {
        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Peek()))
            {
                Advance();
                continue;
            }

            if (Peek() != '-' || !TryPeek(1, out var next) || next != '-')
            {
                return;
            }

            Advance();
            Advance();
            while (!IsAtEnd && Peek() is not ('\r' or '\n'))
            {
                Advance();
            }
        }
    }

    private Token ReadString(int tokenStart)
    {
        var value = new StringBuilder();

        while (!IsAtEnd)
        {
            var current = Advance();
            if (current != '\'')
            {
                value.Append(current);
                continue;
            }

            // SQL escapes an apostrophe inside a string by doubling it.
            if (IsAtEnd || Peek() != '\'') return new Token(TokenType.String, value.ToString());
            
            Advance();
            value.Append('\'');
        }

        throw new LexerException("Unterminated string literal.", tokenStart);
    }

    private Token ReadInteger(char firstCharacter)
    {
        var value = new StringBuilder().Append(firstCharacter);
        while (!IsAtEnd && char.IsDigit(Peek()))
        {
            value.Append(Advance());
        }

        return new Token(TokenType.Digit, value.ToString());
    }

    private Token ReadWord(char firstCharacter)
    {
        var value = new StringBuilder().Append(firstCharacter);
        while (!IsAtEnd && IsIdentifierPart(Peek()))
        {
            value.Append(Advance());
        }

        var text = value.ToString();
        return Keywords.Contains(text)
            ? new Token(TokenType.Keyword, text)
            : new Token(TokenType.Ident, text);
    }

    private static LexerException UnexpectedCharacter(char character, int position)
    {
        return new LexerException($"Unexpected character '{character}'.", position);
    }

    private bool IsAtEnd => _inputIndex >= _input.Length;

    private char Advance() => _input[_inputIndex++];

    private char Peek() => _input[_inputIndex];

    private bool TryPeek(int offset, out char character)
    {
        var index = _inputIndex + offset;
        if (index < _input.Length)
        {
            character = _input[index];
            return true;
        }

        character = default;
        return false;
    }

    private static bool IsIdentifierStart(char character) =>
        char.IsLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character) =>
        char.IsLetterOrDigit(character) || character == '_';
}

public sealed class LexerException(string message, int position)
    : Exception($"{message} (position {position})")
{
    public int Position { get; } = position;
}

public sealed record Token(TokenType TokenType, string Value = "");

public enum TokenType
{
    String,
    Operator,
    Ident,
    Keyword,
    Digit,
    SemiColon,
    Comma
}
