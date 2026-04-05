using BenchmarkDotNet.Running;

// This single line discovers and runs all classes marked [SimpleJob]
BenchmarkRunner.Run<EvaluatorBenchmarks>();