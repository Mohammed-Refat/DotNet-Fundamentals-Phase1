using System.Linq.Expressions;

namespace ExpressionEvaluator;

// ═══════════════════════════════════════════════════════════════
// FIX EXPLANATION:
//
// The bug was two separate static ParameterExpression fields —
// one in ExpressionEvaluator, one in Parser.
//
// Expression Trees use REFERENCE EQUALITY for parameters.
// Same name + same type ≠ same object. The Lambda must be built
// with the EXACT SAME ParameterExpression object used inside the tree.
//
// Fix: ONE static ParameterExpression defined here and PASSED
// into Parser. Single source of truth.
// ═══════════════════════════════════════════════════════════════

public sealed class ExpressionEvaluator
{
    // ───────────────────────────────────────────────────────────
    // THE ONE TRUE PARAMETER — defined once, shared everywhere
    // Parser receives this via constructor, uses it for all
    // variable lookup nodes it builds.
    // ───────────────────────────────────────────────────────────
    internal static readonly ParameterExpression VariablesParam =
        Expression.Parameter(typeof(Dictionary<string, double>), "variables");

    private readonly Dictionary<string, Func<Dictionary<string, double>, double>>
        _cache = new();

    public Func<Dictionary<string, double>, double> Compile(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression, nameof(expression));

        if (_cache.TryGetValue(expression, out var cached))
            return cached;

        var lexer = new Lexer(expression);
        var tokens = lexer.Tokenize();

        // Pass VariablesParam into Parser so it uses the SAME object
        var parser = new Parser(tokens, VariablesParam);
        var expressionTree = parser.Parse();

        // Lambda uses the SAME VariablesParam that Parser used internally
        // Now the compiler can match them — same reference = no error
        var lambda = Expression.Lambda<Func<Dictionary<string, double>, double>>(
            expressionTree,
            VariablesParam      // ← same object Parser used inside the tree
        );

        var compiled = lambda.Compile();
        _cache[expression] = compiled;
        return compiled;
    }

    public double Evaluate(string expression, Dictionary<string, double> variables)
    {
        var compiled = Compile(expression);
        return compiled(variables);
    }

    public int CacheSize => _cache.Count;
    public void ClearCache() => _cache.Clear();
}

internal sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    // ───────────────────────────────────────────────────────────
    // Receive the ParameterExpression from ExpressionEvaluator
    // This guarantees we use the exact same object
    // ───────────────────────────────────────────────────────────
    private readonly ParameterExpression _variablesParam;

    internal Parser(IReadOnlyList<Token> tokens, ParameterExpression variablesParam)
    {
        _tokens = tokens;
        _variablesParam = variablesParam;  // store the shared reference
        _position = 0;
    }

    internal Expression Parse()
    {
        var expr = ParseAddSubtract();

        if (Current.Type != TokenType.End)
            throw new InvalidOperationException(
                $"Unexpected token '{Current.Value}' after expression.");

        return expr;
    }

    private Expression ParseAddSubtract()
    {
        var left = ParseMultiplyDivide();

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Current.Type;
            Advance();
            var right = ParseMultiplyDivide();

            left = op == TokenType.Plus
                ? Expression.Add(left, right)
                : Expression.Subtract(left, right);
        }

        return left;
    }

    private Expression ParseMultiplyDivide()
    {
        var left = ParsePrimary();

        while (Current.Type is TokenType.Multiply or TokenType.Divide)
        {
            var op = Current.Type;
            Advance();
            var right = ParsePrimary();

            left = op == TokenType.Multiply
                ? Expression.Multiply(left, right)
                : Expression.Divide(left, right);
        }

        return left;
    }

    private Expression ParsePrimary()
    {
        var token = Current;

        if (token.Type == TokenType.Number)
        {
            Advance();
            var value = double.Parse(token.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            return Expression.Constant(value, typeof(double));
        }

        if (token.Type == TokenType.Variable)
        {
            Advance();
            return BuildVariableLookup(token.Value);
        }

        if (token.Type == TokenType.LeftParen)
        {
            Advance();
            var inner = ParseAddSubtract();

            if (Current.Type != TokenType.RightParen)
                throw new InvalidOperationException(
                    "Expected closing parenthesis ')'.");

            Advance();
            return inner;
        }

        throw new InvalidOperationException(
            $"Unexpected token '{token.Value}' of type {token.Type}.");
    }

    private Expression BuildVariableLookup(string variableName)
    {
        var indexerMethod = typeof(Dictionary<string, double>)
            .GetProperty("Item")!
            .GetGetMethod()!;

        // Use _variablesParam (the shared instance) — NOT a new one
        return Expression.Call(
            _variablesParam,                        // ← shared reference
            indexerMethod,
            Expression.Constant(variableName)
        );
    }

    private Token Current => _tokens[_position];

    private void Advance()
    {
        if (_position < _tokens.Count - 1)
            _position++;
    }
}