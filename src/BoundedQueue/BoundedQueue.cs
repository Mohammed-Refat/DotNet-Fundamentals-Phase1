
namespace BoundedQueue;

// ═══════════════════════════════════════════════════════════════
// SENIOR NOTE:
// This class implements our interface + IDisposable.
// IDisposable = "this class holds resources that must be
// cleaned up when done" (like our SemaphoreSlim objects)
//
// sealed = no one can inherit from this class.
// Good default for concrete implementations.
// ═══════════════════════════════════════════════════════════════

public sealed class BoundedQueue<T> : IBoundedQueue<T>
{
    // ───────────────────────────────────────────────────────────
    // THE INTERNAL STORAGE
    // Queue<T> is a first-in-first-out collection.
    // We wrap it — outside world uses OUR interface, not Queue directly.
    // ───────────────────────────────────────────────────────────
    private readonly Queue<T> _queue;
    private readonly int _capacity;

    // ───────────────────────────────────────────────────────────
    // SEMAPHORESLIM EXPLAINED:
    //
    // Think of a SemaphoreSlim as a "ticket counter"
    //
    // _slotsAvailable = how many EMPTY slots exist (starts at capacity)
    //   - Enqueue: takes a ticket (WaitAsync) → adds item → ...
    //   - Dequeue: releases a ticket (Release) ← removes item ←
    //
    // _itemsAvailable = how many ITEMS exist (starts at 0)
    //   - Enqueue: releases a ticket (Release) → item added
    //   - Dequeue: takes a ticket (WaitAsync) ← waits for item
    //
    // This is the classic Producer-Consumer synchronization pattern.
    // ───────────────────────────────────────────────────────────
    private readonly SemaphoreSlim _slotsAvailable;  // empty slots
    private readonly SemaphoreSlim _itemsAvailable;  // filled slots

    // Protects the Queue<T> from concurrent access
    // (SemaphoreSlim handles WAITING, this lock handles the actual read/write)
    private readonly object _lock = new();

    // Track if Dispose() was already called
    private bool _disposed;

    // ───────────────────────────────────────────────────────────
    // CONSTRUCTOR
    // ───────────────────────────────────────────────────────────
    public BoundedQueue(int capacity)
    {
        // Guard clause — fail fast with a clear message
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "Capacity must be greater than zero.");

        _capacity = capacity;
        _queue = new Queue<T>(capacity);

        // Start: all slots available (capacity), no items yet (0)
        _slotsAvailable = new SemaphoreSlim(capacity, capacity);
        _itemsAvailable = new SemaphoreSlim(0, capacity);
    }

    // ───────────────────────────────────────────────────────────
    // PROPERTIES
    // ───────────────────────────────────────────────────────────
    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_lock) return _queue.Count;
        }
    }

    public bool IsEmpty => Count == 0;
    public bool IsFull => Count == _capacity;

    // ───────────────────────────────────────────────────────────
    // ENQUEUE ASYNC
    // This is where async/await gets real. Read every comment.
    // ───────────────────────────────────────────────────────────
    public async Task<bool> EnqueueAsync(T item, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // STEP 1: Wait for an empty slot to be available.
        // WaitAsync = "wait asynchronously until I can take a slot ticket"
        // If queue is full → this line PAUSES here (no thread blocked!)
        // and resumes ONLY when Dequeue frees a slot.
        //
        // WaitAsync returns false if cancelled before getting a slot.
        bool acquired = await _slotsAvailable.WaitAsync(
            millisecondsTimeout: -1,  // wait forever (until cancelled)
            cancellationToken: ct
        ).ConfigureAwait(false);
        // ConfigureAwait(false) = don't need to come back on original thread.
        // Always use this in library/non-UI code. Performance best practice.

        if (!acquired) return false; // was cancelled

        // STEP 2: We have a slot — safely add the item
        lock (_lock)
        {
            _queue.Enqueue(item);
        }

        // STEP 3: Signal that an item is now available for consumers
        _itemsAvailable.Release();

        return true;
    }

    // ───────────────────────────────────────────────────────────
    // DEQUEUE ASYNC
    // ───────────────────────────────────────────────────────────
    public async Task<T> DequeueAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // STEP 1: Wait until at least one item exists
        // If queue is empty → pauses here until Enqueue adds something
        await _itemsAvailable.WaitAsync(ct).ConfigureAwait(false);

        // STEP 2: Safely remove and return the item
        T item;
        lock (_lock)
        {
            item = _queue.Dequeue();
        }

        // STEP 3: Signal that a slot is now free for producers
        _slotsAvailable.Release();

        return item;
    }

    // ───────────────────────────────────────────────────────────
    // SYNCHRONOUS (non-waiting) VARIANTS
    // These return immediately — useful for "try if possible" scenarios
    // ───────────────────────────────────────────────────────────
    public bool TryEnqueue(T item)
    {
        // WaitAsync(0) = try to take a slot ticket RIGHT NOW, no waiting
        if (!_slotsAvailable.Wait(millisecondsTimeout: 0))
            return false; // queue is full, give up

        lock (_lock)
        {
            _queue.Enqueue(item);
        }

        _itemsAvailable.Release();
        return true;
    }

    public bool TryDequeue(out T? item)
    {
        if (!_itemsAvailable.Wait(millisecondsTimeout: 0))
        {
            item = default;
            return false; // queue is empty, give up
        }

        lock (_lock)
        {
            item = _queue.Dequeue();
        }

        _slotsAvailable.Release();
        return true;
    }

    // ───────────────────────────────────────────────────────────
    // IDISPOSABLE IMPLEMENTATION
    // SENIOR NOTE:
    // Any class that holds SemaphoreSlim (or any IDisposable field)
    // MUST implement IDisposable and Dispose() those fields.
    // If you forget this → memory leak / resource leak in production.
    // ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return; // safe to call multiple times
        _disposed = true;

        _slotsAvailable.Dispose();
        _itemsAvailable.Dispose();

        // SENIOR NOTE: GC.SuppressFinalize(this) tells the garbage
        // collector "no need to call the finalizer, we already cleaned up"
        // This is standard IDisposable best practice.
        GC.SuppressFinalize(this);
    }
    public IEnumerable<T> Snapshot()
    {
        lock (_lock)
        {
            return _queue.ToList();
        }
    }
}