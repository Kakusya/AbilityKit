using System.Collections.Concurrent;

namespace AbilityKit.Game.Cooking.NetworkAcceptance;

// ET requires all owner operations on the constructing thread. Network callbacks
// stay on their transport threads; awaited scenario continuations return here.
internal sealed class SingleThreadOwner : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    public override void Post(SendOrPostCallback callback, object? state) => _queue.Add((callback, state));

    public static int Run(Func<Task<int>> scenario)
    {
        var previous = Current;
        var owner = new SingleThreadOwner();
        SetSynchronizationContext(owner);
        try
        {
            var task = scenario();
            while (!task.IsCompleted)
                if (owner._queue.TryTake(out var continuation, 100)) continuation.Callback(continuation.State);
            return task.GetAwaiter().GetResult();
        }
        finally { SetSynchronizationContext(previous); }
    }
}
