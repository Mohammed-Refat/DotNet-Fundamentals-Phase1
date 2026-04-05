using BenchmarkDotNet.Attributes;
using ExpressionEvaluator;
using Perfolizer.Horology;
using System.Collections.Generic;
using System.Text;

[MemoryDiagnoser]  // shows memory allocations per operation
[SimpleJob]        // runs with default settings
public class EvaluatorBenchmarks
{
    private ExpressionEvaluator.ExpressionEvaluator _evaluator = null!;
    private Func<Dictionary<string, double>, double> _compiledDelegate = null!;
    private Dictionary<string, double> _variables = null!;
    private const string Expression = "x * 2 + y - z / 3";

    [GlobalSetup]
    public void Setup()
    {
        _evaluator = new ExpressionEvaluator.ExpressionEvaluator();
        _variables = new Dictionary<string, double>
        {
            ["x"] = 5,
            ["y"] = 3,
            ["z"] = 9
        };

        // Pre-compile for the cached benchmark
        _compiledDelegate = _evaluator.Compile(Expression);
    }

    // Benchmark 1: Parse + compile + evaluate every single call (worst case)
    [Benchmark(Baseline = true, Description = "Parse + Compile + Evaluate each time")]
    public double ParseAndEvaluateEveryTime()
    {
        // Create new evaluator each time = no cache = full parse every call
        var freshEvaluator = new ExpressionEvaluator.ExpressionEvaluator();
        return freshEvaluator.Evaluate(Expression, _variables);
    }

    // Benchmark 2: Use cached compile result (best case — what you should do)
    [Benchmark(Description = "Compiled delegate (cached)")]
    public double UseCachedDelegate()
    {
        return _compiledDelegate(_variables);
    }

    // Benchmark 3: Use evaluator with cache (realistic production scenario)
    [Benchmark(Description = "Evaluator with cache hit")]
    public double EvaluatorWithCacheHit()
    {
        return _evaluator.Evaluate(Expression, _variables);
    }
}