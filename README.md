# .NET Backend Engineering: Phase 1 Assessments

This repository contains the practical implementations for Phase 1 of my .NET Backend training. The focus is on mastering C# deep internals, multi-threading, and advanced language features.

## 🚀 Projects Included

### 1. Custom Generic Bounded Blocking Queue
* **Goal:** A thread-safe, bounded blocking queue implemented using low-level synchronization primitives.
* **Key Features:** * Thread safety using `SemaphoreSlim` and `lock`.
    * Supports asynchronous `Enqueue` and `Dequeue` operations.
    * Implements `IDisposable` for proper resource management.
* **Concepts:** Generics, Concurrency, Async/Await Patterns.

### 2. Mini Expression Evaluator
* **Goal:** A tool to parse mathematical string expressions and compile them into executable code at runtime.
* **Key Features:**
    * High-performance evaluation using **Expression Trees**.
    * Comparison benchmarks against standard Reflection.
* **Concepts:** Delegates (`Func<T>`), Expression Trees, Meta-programming.

## 🛠 Tech Stack
* **Language:** C# 12 / .NET 8
* **Environment:** PowerShell, Cursor IDE
* **Testing:** xUnit
* **Benchmarking:** BenchmarkDotNet

## ⚙️ How to Run
1. Clone the repository:
   ```powershell
   git clone [https://github.com/Mohammed-Refat/DotNet-Fundamentals-Phase1.git](https://github.com/Mohammed-Refat/DotNet-Fundamentals-Phase1.git)
