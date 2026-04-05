namespace BoundedQueue;

// ═══════════════════════════════════════════════════════════════
// SENIOR NOTE:
// We define a contract (interface) before implementation.
// This is how professional code is written — always program
// to abstractions, not concrete classes.
//
// GENERICS EXPLAINED:
// <T> means "this works with ANY type the caller decides"
// BoundedQueue<int>, BoundedQueue<string>, BoundedQueue<Order>
// — same code, zero duplication. That's the power of generics.
// ═══════════════════════════════════════════════════════════════

public interface IBoundedQueue<T> : IDisposable
{
    // How many items are currently in the queue
    int Count { get; }

    // Maximum items this queue can hold
    int Capacity { get; }

    // Is the queue empty?
    bool IsEmpty { get; }

    // Is the queue full?
    bool IsFull { get; }

    // ───────────────────────────────────────────────────────────
    // ASYNC/AWAIT EXPLAINED (read this carefully):
    //
    // Task<bool> means: "this operation runs asynchronously
    // and will EVENTUALLY return a bool result"
    //
    // Why async here?
    // If the queue is FULL, Enqueue must WAIT for space.
    // If the queue is EMPTY, Dequeue must WAIT for an item.
    // Waiting = async. We never block a thread — we free it
    // while waiting. This is the professional way.
    //
    // CancellationToken = a way to say "stop waiting, cancel"
    // e.g. user cancels request, timeout reached, app shutting down
    // ───────────────────────────────────────────────────────────

    // Add item — waits if queue is full
    // Returns true if enqueued, false if cancelled
    Task<bool> EnqueueAsync(T item, CancellationToken ct = default);

    // Remove and return item — waits if queue is empty
    // Returns the item when available
    Task<T> DequeueAsync(CancellationToken ct = default);

    // Try to add without waiting — returns false immediately if full
    bool TryEnqueue(T item);

    // Try to remove without waiting — returns false immediately if empty
    bool TryDequeue(out T? item);

    IEnumerable<T> Snapshot();
    
}