namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncStateTrackerTests : Test
{
    [Fact]
    public static async Task NewerTokenAvailableAfterCompletion()
    {
        var tracker = new AsyncStateTracker { IsNewerTokenAvailableAfterCompletion = false };
        False(tracker.IsNewerTokenAvailableAfterCompletion);
        var currentToken = tracker.CurrentState;
        False(tracker.IsCompleted);
        True(tracker.TryAdvance());
        True(tracker.TryComplete());
        True(tracker.IsCompleted);

        False(await tracker.WaitNextAsync(currentToken, TestToken));

        tracker = new() { IsNewerTokenAvailableAfterCompletion = true };
        True(tracker.IsNewerTokenAvailableAfterCompletion);
        currentToken = tracker.CurrentState;
        True(tracker.TryAdvance());
        True(tracker.TryComplete());
        
        True(await tracker.WaitNextAsync(currentToken, TestToken));
        currentToken = tracker.CurrentState;
        False(await tracker.WaitNextAsync(currentToken, TestToken));
    }

    [Fact]
    public static async Task StaleToken()
    {
        var tracker = new AsyncStateTracker();
        var currentToken = tracker.CurrentState;
        
        True(tracker.TryAdvance());
        True(await tracker.WaitNextAsync(currentToken, TestToken));
        
        True(tracker.TryAdvance());
        True(await tracker.WaitNextAsync(currentToken, TestToken));
    }

    [Fact]
    public static async Task ResumeOnAdvance()
    {
        var tracker = new AsyncStateTracker();
        True(tracker.TryAdvance(out var resumed));
        False(resumed);

        var task = tracker.WaitNextAsync(tracker.CurrentState, TestToken).AsTask();
        True(tracker.TryAdvance(out resumed));
        True(resumed);
        True(await task);
    }
    
    [Fact]
    public static async Task ResumeOnCompletion()
    {
        var tracker = new AsyncStateTracker();
        True(tracker.TryAdvance(out var resumed));
        False(resumed);

        var task = tracker.WaitNextAsync(tracker.CurrentState, TestToken).AsTask();
        True(tracker.TryComplete(out resumed));
        True(resumed);
        False(await task);
    }

    [Fact]
    public static void AdvanceAfterCompletion()
    {
        var tracker = new AsyncStateTracker();
        True(tracker.TryComplete());
        False(tracker.TryComplete());
        False(tracker.TryAdvance());
    }

    [Fact]
    public static void ConcurrencyLevel()
    {
        var tracker = new AsyncStateTracker { ConcurrencyLevel = 10 };
        Equal(10, tracker.ConcurrencyLevel);
    }

    [Fact]
    public static async Task ProduceStream()
    {
        var tracker = new AsyncStateTracker();

        var expected = 0;
        await foreach (var actual in GetStream())
        {
            Equal(expected++, actual);
            True(expected < 20 ? tracker.TryAdvance() : tracker.TryComplete());
        }

        async IAsyncEnumerable<int> GetStream()
        {
            AsyncStateTracker.Token state;
            var i = 0;
            do
            {
                state = tracker.CurrentState;
                yield return i++;
            } while (await tracker.WaitNextAsync(state, TestToken));
        }
    }
}