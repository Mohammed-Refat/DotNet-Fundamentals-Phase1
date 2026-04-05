using BenchmarkDotNet.Attributes;
using ExpressionEvaluator;

// ═══════════════════════════════════════════════════════════════
// TESTS
// ═══════════════════════════════════════════════════════════════

public class LexerTests
{
    [Fact]
    public void Tokenize_SimpleExpression_ReturnsCorrectTokens()
    {
        // Arrange
        var lexer = new Lexer("x + 2");

        // Act
        var tokens = lexer.Tokenize();

        // Assert — check each token type and value
        Assert.Equal(TokenType.Variable, tokens[0].Type);
        Assert.Equal("x", tokens[0].Value);

        Assert.Equal(TokenType.Plus, tokens[1].Type);

        Assert.Equal(TokenType.Number, tokens[2].Type);
        Assert.Equal("2", tokens[2].Value);

        Assert.Equal(TokenType.End, tokens[3].Type);
    }

    [Fact]
    public void Tokenize_WithWhitespace_IgnoresSpaces()
    {
        var lexer = new Lexer("  x   *   y  ");
        var tokens = lexer.Tokenize();

        // Only 3 real tokens + End (whitespace ignored)
        Assert.Equal(4, tokens.Count);
        Assert.Equal(TokenType.Variable, tokens[0].Type);
        Assert.Equal(TokenType.Multiply, tokens[1].Type);
        Assert.Equal(TokenType.Variable, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_WithDecimalNumber_ParsesCorrectly()
    {
        var lexer = new Lexer("3.14");
        var tokens = lexer.Tokenize();

        Assert.Equal(TokenType.Number, tokens[0].Type);
        Assert.Equal("3.14", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_InvalidCharacter_ThrowsInvalidOperationException()
    {
        var lexer = new Lexer("x @ y");
        Assert.Throws<InvalidOperationException>(() => lexer.Tokenize());
    }
}

public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator.ExpressionEvaluator _evaluator = new();

    // ─────────────────────────────────────────────────────
    // BASIC ARITHMETIC
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Addition_ReturnsCorrectResult()
    {
        var result = _evaluator.Evaluate("x + y", Vars(("x", 3), ("y", 4)));
        Assert.Equal(7.0, result);
    }

    [Fact]
    public void Evaluate_Subtraction_ReturnsCorrectResult()
    {
        var result = _evaluator.Evaluate("x - y", Vars(("x", 10), ("y", 4)));
        Assert.Equal(6.0, result);
    }

    [Fact]
    public void Evaluate_Multiplication_ReturnsCorrectResult()
    {
        var result = _evaluator.Evaluate("x * 2", Vars(("x", 5)));
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void Evaluate_Division_ReturnsCorrectResult()
    {
        var result = _evaluator.Evaluate("x / 4", Vars(("x", 20)));
        Assert.Equal(5.0, result);
    }

    // ─────────────────────────────────────────────────────
    // OPERATOR PRECEDENCE — critical tests
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_MultiplyBeforeAdd_RespectsOperatorPrecedence()
    {
        // "x + y * 2" must be "x + (y * 2)", NOT "(x + y) * 2"
        // x=1, y=3 → 1 + (3*2) = 7, NOT (1+3)*2 = 8
        var result = _evaluator.Evaluate("x + y * 2", Vars(("x", 1), ("y", 3)));
        Assert.Equal(7.0, result);
    }

    [Fact]
    public void Evaluate_Parentheses_OverridesOperatorPrecedence()
    {
        // "(x + y) * 2" forces addition first
        // x=1, y=3 → (1+3)*2 = 8
        var result = _evaluator.Evaluate("(x + y) * 2", Vars(("x", 1), ("y", 3)));
        Assert.Equal(8.0, result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_ReturnsCorrectResult()
    {
        // x * 2 + y → (5*2) + 3 = 13
        var result = _evaluator.Evaluate("x * 2 + y", Vars(("x", 5), ("y", 3)));
        Assert.Equal(13.0, result);
    }

    [Fact]
    public void Evaluate_NestedParentheses_ReturnsCorrectResult()
    {
        // (x + (y * 2)) / 2 → (3 + (4*2)) / 2 → (3+8)/2 = 5.5
        var result = _evaluator.Evaluate(
            "(x + (y * 2)) / 2",
            Vars(("x", 3), ("y", 4)));
        Assert.Equal(5.5, result);
    }

    // ─────────────────────────────────────────────────────
    // COMPILE ONCE CACHE — critical behavior
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Compile_SameExpressionTwice_ReturnsSameDelegate()
    {
        // SENIOR NOTE: This verifies the cache works correctly.
        // Same expression string → same compiled delegate object
        var delegate1 = _evaluator.Compile("x + y");
        var delegate2 = _evaluator.Compile("x + y");

        // ReferenceEquals = they are the EXACT same object in memory
        Assert.True(ReferenceEquals(delegate1, delegate2));
        Assert.Equal(1, _evaluator.CacheSize); // only compiled once
    }

    [Fact]
    public void Compile_DifferentExpressions_CachesBoth()
    {
        _evaluator.Compile("x + y");
        _evaluator.Compile("x * y");

        Assert.Equal(2, _evaluator.CacheSize);
    }

    [Fact]
    public void Compile_CompiledDelegate_CanBeCalledMultipleTimesWithDifferentVars()
    {
        // This is the KEY use case — compile once, call many times
        var calculate = _evaluator.Compile("x * 2 + y");

        var result1 = calculate(Vars(("x", 5), ("y", 3)));   // 13
        var result2 = calculate(Vars(("x", 10), ("y", 7)));  // 27
        var result3 = calculate(Vars(("x", 0), ("y", 0)));   // 0

        Assert.Equal(13.0, result1);
        Assert.Equal(27.0, result2);
        Assert.Equal(0.0, result3);
    }

    // ─────────────────────────────────────────────────────
    // EDGE CASES
    // ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_SingleVariable_ReturnsVariableValue()
    {
        var result = _evaluator.Evaluate("x", Vars(("x", 42)));
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void Evaluate_SingleNumber_ReturnsNumber()
    {
        var result = _evaluator.Evaluate("42", new Dictionary<string, double>());
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void Evaluate_DecimalNumbers_WorksCorrectly()
    {
        var result = _evaluator.Evaluate("x * 3.14", Vars(("x", 2)));
        Assert.Equal(6.28, result, precision: 10);
    }

    [Fact]
    public void Evaluate_MissingVariable_ThrowsKeyNotFoundException()
    {
        // If you reference "z" but don't provide it → KeyNotFoundException
        // SENIOR NOTE: We use Assert.ThrowsAny here because the compiled
        // delegate may wrap the exception depending on .NET version.
        // The inner exception is always KeyNotFoundException.
        var ex = Assert.ThrowsAny<Exception>(() =>
            _evaluator.Evaluate("x + z", Vars(("x", 1))));

        // Walk the exception chain to find KeyNotFoundException
        var inner = ex;
        while (inner is not null && inner is not KeyNotFoundException)
            inner = inner.InnerException;

        Assert.IsType<KeyNotFoundException>(inner);
    }

    [Fact]
    public void Compile_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _evaluator.Compile(null!));
    }

    [Fact]
    public void Compile_WhitespaceExpression_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _evaluator.Compile("   "));
    }

    // ─────────────────────────────────────────────────────
    // YOUR TURN — Write this test yourself:
    //
    // TODO: Write a test called:
    // Evaluate_ChainedOperations_ReturnsCorrectResult
    //
    // Test this expression: "a + b - c * d / 2"
    // With: a=10, b=5, c=4, d=3
    // Expected: 10 + 5 - (4*3)/2 = 10 + 5 - 6 = 9
    // ─────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────
    // HELPER — makes test setup cleaner
    // Instead of: new Dictionary<string,double> { ["x"]=1, ["y"]=2 }
    // We write:   Vars(("x",1), ("y",2))

    [Fact]
    public void Evaluate_ChainedOperations_ReturnsCorrectResult()
    {
        var result = _evaluator.Evaluate("a + b - c * d / 2", Vars(("a", 10),("b",5),("c",4),("d",3)));
        Assert.Equal(9, result);
    }


    // ─────────────────────────────────────────────────────
    private static Dictionary<string, double> Vars(
        params (string name, double value)[] variables)
        => variables.ToDictionary(v => v.name, v => v.value);
}

// ═══════════════════════════════════════════════════════════════
// BENCHMARK
//
// SENIOR NOTE:
// This proves WHY compile-once matters.
// You will see a massive speed difference between:
//   - Parsing the string every time (slow)
//   - Using a compiled delegate (fast)
//
// TO RUN BENCHMARKS:
//   1. Change project to Release mode (not Debug)
//   2. In Program.cs (or a console app) call:
//      BenchmarkRunner.Run<EvaluatorBenchmarks>();
//   3. Read the results table
// ═══════════════════════════════════════════════════════════════

