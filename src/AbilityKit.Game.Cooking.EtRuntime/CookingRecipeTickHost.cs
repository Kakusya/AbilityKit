using global::ET;
using ArgumentNullException = System.ArgumentNullException;

namespace AbilityKit.Game.Cooking.EtRuntime;

public sealed class CookingRecipeTickHost : IDisposable
{
    private readonly EtRuntimeHost _runtime;
    private readonly CookingRecipeSimulation _simulation;
    private readonly Queue<CookingRecipeCommand> _pending = new();
    private readonly List<CookingRecipeCommandResult> _results = new();
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private Exception? _tickFailure;
    private bool _disposed;
    private bool _ticking;

    public CookingRecipeTickHost(CookingRecipeSimulation simulation)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _runtime = new EtRuntimeHost(typeof(CookingRecipeTickHost).Assembly);
        try
        {
            _runtime.CreateScene(1, "cooking-recipe");
            _runtime.Run(1, scene => scene.AddChild<CookingRecipeDriver>().Host = this);
        }
        catch
        {
            _runtime.Dispose();
            throw;
        }
    }

    public void Enqueue(CookingRecipeCommand command)
    {
        Check();
        ArgumentNullException.ThrowIfNull(command);
        if (_pending.Count >= 256)
            throw new InvalidOperationException("Recipe command queue is full.");
        _pending.Enqueue(command);
    }

    public IReadOnlyList<CookingRecipeCommandResult> Tick()
    {
        Check();
        _ticking = true;
        _results.Clear();
        try
        {
            _runtime.Tick();
            if (_tickFailure is not null)
                throw new InvalidOperationException("Recipe authority failed during the ET tick; the host is faulted.", _tickFailure);
            return _results.ToArray();
        }
        finally
        {
            _ticking = false;
        }
    }

    internal void ExecutePending()
    {
        // ET catches system exceptions; capture authority failures so Tick cannot report success.
        try
        {
            while (_pending.TryDequeue(out var command))
                _results.Add(_simulation.Submit(command));
        }
        catch (Exception exception)
        {
            _tickFailure = exception;
        }
    }

    private void Check()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Recipe host operations require the owner thread.");
        if (_ticking)
            throw new InvalidOperationException("Recipe host operations cannot be reentered.");
        if (_tickFailure is not null)
            throw new InvalidOperationException("Recipe host is faulted.", _tickFailure);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _ownerThread || _ticking)
            throw new InvalidOperationException("Dispose requires the idle owner thread.");
        _runtime.Dispose();
        _simulation.CloseLifecycle();
        _pending.Clear();
        _disposed = true;
    }
}

internal sealed class CookingRecipeDriver : Entity, IAwake, IUpdate
{
    public CookingRecipeTickHost Host { get; set; } = null!;
}

[EntitySystem]
internal sealed class CookingRecipeDriverUpdate : UpdateSystem<CookingRecipeDriver>
{
    protected override void Update(CookingRecipeDriver self) => self.Host.ExecutePending();
}
