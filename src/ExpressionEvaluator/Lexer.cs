namespace ExpressionEvaluator;

// ═══════════════════════════════════════════════════════════════
// STEP 1: TOKENIZER (LEXER)
//
// SENIOR NOTE:
// Before we can build an expression tree, we need to break
// the raw string into meaningful pieces called TOKENS.
//
// "x * 2 + y"  becomes:
//  [Variable:"x"] [Multiply] [Number:2] [Plus] [Variable:"y"]
//
// This is called LEXICAL ANALYSIS — the first step of any
// compiler or interpreter. Real compilers (Roslyn for C#)
// do exactly this as their first phase.
// ═══════════════════════════════════════════════════════════════

// Represents what KIND of token we found
public enum TokenType
{
    Number,      // 42, 3.14, 0.5
    Variable,    // x, y, myVar
    Plus,        // +
    Minus,       // -
    Multiply,    // *
    Divide,      // /
    LeftParen,   // (
    RightParen,  // )
    End          // marks end of input
}

// A single token — its type and raw text value
public sealed record Token(TokenType Type, string Value)
{
    // Convenience: is this token an operator?
    public bool IsOperator =>
        Type is TokenType.Plus or TokenType.Minus
               or TokenType.Multiply or TokenType.Divide;
}

// ───────────────────────────────────────────────────────────────
// THE LEXER
// Walks through the input string character by character
// and produces a list of tokens
// ───────────────────────────────────────────────────────────────
public sealed class Lexer
{
    private readonly string _input;
    private int _position;

    public Lexer(string input)
    {
        // Guard clause — always validate inputs
        ArgumentException.ThrowIfNullOrWhiteSpace(input, nameof(input));
        _input = input.Trim();
        _position = 0;
    }

    // Returns ALL tokens from the input string
    public IReadOnlyList<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_position < _input.Length)
        {
            // Skip whitespace — spaces don't matter in math expressions
            if (char.IsWhiteSpace(Current))
            {
                _position++;
                continue;
            }

            // Try to read each possible token type
            var token = Current switch
            {
                '+' => ReadSingleChar(TokenType.Plus),
                '-' => ReadSingleChar(TokenType.Minus),
                '*' => ReadSingleChar(TokenType.Multiply),
                '/' => ReadSingleChar(TokenType.Divide),
                '(' => ReadSingleChar(TokenType.LeftParen),
                ')' => ReadSingleChar(TokenType.RightParen),

                // If it starts with a digit or dot → it's a number
                char c when char.IsDigit(c) || c == '.' => ReadNumber(),

                // If it starts with a letter → it's a variable name
                char c when char.IsLetter(c) => ReadVariable(),

                // Anything else is invalid
                _ => throw new InvalidOperationException(
                    $"Unexpected character '{Current}' at position {_position}")
            };

            tokens.Add(token);
        }

        // Always end with an End token — parser uses this to know when to stop
        tokens.Add(new Token(TokenType.End, string.Empty));
        return tokens;
    }

    // ───────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ───────────────────────────────────────────────────────────

    // Current character we're looking at
    private char Current => _input[_position];

    // Read a single-character token (+, -, *, /, etc.)
    private Token ReadSingleChar(TokenType type)
    {
        var value = Current.ToString();
        _position++;
        return new Token(type, value);
    }

    // Read a full number like "3.14" or "42"
    private Token ReadNumber()
    {
        int start = _position;

        while (_position < _input.Length &&
               (char.IsDigit(Current) || Current == '.'))
        {
            _position++;
        }

        var value = _input[start.._position]; // range operator (C# 8+)
        return new Token(TokenType.Number, value);
    }

    // Read a full variable name like "x" or "myVar" or "total"
    private Token ReadVariable()
    {
        int start = _position;

        // Variable names: start with letter, can contain letters/digits
        while (_position < _input.Length &&
               (char.IsLetterOrDigit(Current) || Current == '_'))
        {
            _position++;
        }

        var value = _input[start.._position];
        return new Token(TokenType.Variable, value);
    }
}