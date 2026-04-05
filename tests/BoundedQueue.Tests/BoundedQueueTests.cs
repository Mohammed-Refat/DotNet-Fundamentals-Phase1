using BoundedQueue;

// ???????????????????????????????????????????????????????????????
// SENIOR NOTE ON TESTING:
// Every test follows the AAA pattern:
//   Arrange — set up what you need
//   Act     — call the thing you're testing
//   Assert  — verify the result
//
// Test names follow: MethodName_Scenario_ExpectedBehavior
// This makes failures self-documenting.
// ???????????????????????????????????????????????????????????????

public class BoundedQueueTests
{
    // ???????????????????????????????????????????????????????????
    // BASIC BEHAVIOR TESTS
    // ???????????????????????????????????????????????????????????

    [Fact]
    public void Constructor_WithValidCapacity_SetsCapacityCorrectly()
    {
        // Arrange + Act
        using var queue = new BoundedQueue<int>(10);

        // Assert
        Assert.Equal(10, queue.Capacity);
        Assert.Equal(0, queue.Count);
        Assert.True(queue.IsEmpty);
        Assert.False(queue.IsFull);
    }

    [Fact]
    public async Task Snapshot_ReturnsAllCurrentItems_WithoutRemoving()
    {
        //Arrange
        using var queue = new BoundedQueue<int>(5);

        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        await queue.EnqueueAsync(3);

        // Act 
        var snapshot = queue.Snapshot();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, snapshot);   // snapshot contains items
        Assert.False(queue.IsEmpty);                 // queue is NOT empty
        Assert.Equal(3, queue.Count);                // still has 3 items

        // Verify items are still there by dequeuing
        Assert.Equal(1, await queue.DequeueAsync());
        Assert.Equal(2, await queue.DequeueAsync());
        Assert.Equal(3, await queue.DequeueAsync());
        Assert.True(queue.IsEmpty);                  // now empty after consuming

    }

    [Fact]
    public void Constructor_WithZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Assert.Throws verifies that the code DOES throw the expected exception
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BoundedQueue<int>(0));
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BoundedQueue<int>(-5));
    }

    [Fact]
    public async Task EnqueueAsync_SingleItem_CountBecomesOne()
    {
        // Arrange
        using var queue = new BoundedQueue<string>(5);

        // Act
        await queue.EnqueueAsync("hello");

        // Assert
        Assert.Equal(1, queue.Count);
        Assert.False(queue.IsEmpty);
    }

    [Fact]
    public async Task DequeueAsync_AfterEnqueue_ReturnsCorrectItem()
    {
        // Arrange
        using var queue = new BoundedQueue<string>(5);
        await queue.EnqueueAsync("world");

        // Act
        var result = await queue.DequeueAsync();

        // Assert
        Assert.Equal("world", result);
        Assert.Equal(0, queue.Count);
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public async Task EnqueueDequeue_MultipleItems_MaintainsFIFOOrder()
    {
        // FIFO = First In, First Out
        // Arrange
        using var queue = new BoundedQueue<int>(5);

        // Act
        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        await queue.EnqueueAsync(3);

        // Assert — must come out in same order
        Assert.Equal(1, await queue.DequeueAsync());
        Assert.Equal(2, await queue.DequeueAsync());
        Assert.Equal(3, await queue.DequeueAsync());
    }

    [Fact]
    public async Task EnqueueAsync_FillToCapacity_IsFull()
    {
        // Arrange
        using var queue = new BoundedQueue<int>(3);

        // Act
        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);
        await queue.EnqueueAsync(3);

        // Assert
        Assert.True(queue.IsFull);
        Assert.Equal(3, queue.Count);
    }

    // ???????????????????????????????????????????????????????????
    // CANCELLATION TESTS
    // These test the async/CancellationToken behavior
    // ???????????????????????????????????????????????????????????

    [Fact]
    public async Task DequeueAsync_OnEmptyQueueWithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var queue = new BoundedQueue<int>(5);

        // CancellationTokenSource lets us create and trigger cancellation
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel IMMEDIATELY

        // Act + Assert
        // An already-cancelled token should throw right away
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await queue.DequeueAsync(cts.Token));
    }

    [Fact]
    public async Task EnqueueAsync_OnFullQueueWithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange — fill the queue to capacity
        using var queue = new BoundedQueue<int>(2);
        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert — queue is full, token is cancelled ? should throw
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await queue.EnqueueAsync(99, cts.Token));
    }

    [Fact]
    public async Task EnqueueAsync_OnFullQueue_WaitsUntilSpaceAvailable()
    {
        // This tests the CORE async behavior:
        // Producer waits when queue is full, then proceeds after consumer dequeues

        // Arrange
        using var queue = new BoundedQueue<int>(1);
        await queue.EnqueueAsync(42); // fill it

        // Act
        // Start a producer that will BLOCK waiting for space
        var produceTask = queue.EnqueueAsync(99);

        // Give the producer a moment to start waiting
        await Task.Delay(50);

        // Producer should still be waiting (not completed) 
        Assert.False(produceTask.IsCompleted);

        // Now free up space — producer should unblock
        var dequeued = await queue.DequeueAsync();

        // Wait for producer to finish
        await produceTask;

        // Assert
        Assert.Equal(42, dequeued);         // first item came out
        Assert.Equal(1, queue.Count);       // second item is now in
        Assert.Equal(99, await queue.DequeueAsync()); // verify it's 99
    }

    // ???????????????????????????????????????????????????????????
    // CONCURRENT (THREAD-SAFE) TESTS
    // This is where we test the real-world scenario:
    // multiple producers and consumers at the same time
    // ???????????????????????????????????????????????????????????

    [Fact]
    public async Task EnqueueDequeue_MultipleProducersAndConsumers_AllItemsProcessed()
    {
        // Arrange
        const int capacity = 10;
        const int totalItems = 100;
        using var queue = new BoundedQueue<int>(capacity);
        var consumedItems = new System.Collections.Concurrent.ConcurrentBag<int>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var countdown = new CountdownEvent(totalItems);

        // Act — 3 producers
        var producers = Enumerable.Range(0, 3).Select(p =>
            Task.Run(async () =>
            {
                for (int i = p; i < totalItems; i += 3)
                {
                    await queue.EnqueueAsync(i, cts.Token);
                }
            }));

        // 3 consumers — run until all items are consumed
        var consumers = Enumerable.Range(0, 3).Select(c =>
            Task.Run(async () =>
            {
                for (int i = c; i < totalItems; i += 3)
                {
                    var item = await queue.DequeueAsync(cts.Token);
                    consumedItems.Add(item);
                }
            }));

        // Wait for producers and consumers to finish
        await Task.WhenAll(producers.Concat(consumers));

        // Assert — every item was consumed exactly once
        Assert.Equal(totalItems, consumedItems.Count);
        Assert.Equal(
            Enumerable.Range(0, totalItems).OrderBy(x => x),
            consumedItems.OrderBy(x => x)
        );
    }


    // ???????????????????????????????????????????????????????????
    // TRY (NON-WAITING) VARIANTS
    // ???????????????????????????????????????????????????????????

    [Fact]
    public void TryEnqueue_WhenNotFull_ReturnsTrueAndAddsItem()
    {
        using var queue = new BoundedQueue<int>(5);

        var result = queue.TryEnqueue(42);

        Assert.True(result);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public async Task TryEnqueue_WhenFull_ReturnsFalse()
    {
        using var queue = new BoundedQueue<int>(2);
        await queue.EnqueueAsync(1);
        await queue.EnqueueAsync(2);

        var result = queue.TryEnqueue(3);

        Assert.False(result);
        Assert.Equal(2, queue.Count); // still 2, not 3
    }

    [Fact]
    public async Task TryDequeue_WhenNotEmpty_ReturnsTrueWithItem()
    {
        using var queue = new BoundedQueue<int>(5);
        await queue.EnqueueAsync(77);

        var result = queue.TryDequeue(out var item);

        Assert.True(result);
        Assert.Equal(77, item);
    }

    [Fact]
    public void TryDequeue_WhenEmpty_ReturnsFalse()
    {
        using var queue = new BoundedQueue<int>(5);

        var result = queue.TryDequeue(out var item);

        Assert.False(result);
        Assert.Equal(default, item);
    }

    // ???????????????????????????????????????????????????????????
    // DISPOSE TESTS
    // ???????????????????????????????????????????????????????????

    [Fact]
    public async Task EnqueueAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var queue = new BoundedQueue<int>(5);
        queue.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await queue.EnqueueAsync(1));
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Safe to call Dispose multiple times — this is a contract guarantee
        var queue = new BoundedQueue<int>(5);
        var ex = Record.Exception(() =>
        {
            queue.Dispose();
            queue.Dispose(); // second call should be safe
        });

        Assert.Null(ex); // no exception thrown
    }
}