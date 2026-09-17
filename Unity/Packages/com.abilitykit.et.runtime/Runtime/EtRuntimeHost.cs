using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace ET
{
    public sealed class EtRuntimeHost : IDisposable
    {
        private static int active;
        private readonly int ownerThread = Environment.CurrentManagedThreadId;
        private readonly SortedDictionary<int, Fiber> fibers = new SortedDictionary<int, Fiber>();
        private readonly World world;
        private bool disposed;
        private bool executing;

        public EtRuntimeHost(params Assembly[] assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            foreach (Assembly assembly in assemblies)
                if (assembly == null) throw new ArgumentException("Assembly must not be null.", nameof(assemblies));
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
                throw new InvalidOperationException("Only one ET runtime host may be active per process.");
            try
            {
                world = World.Instance;
                world.AddSingleton(new Options { Process = 1 });
                world.AddSingleton<Logger>();
                world.AddSingleton<TimeInfo>();
                world.AddSingleton<ObjectPool>();
                world.AddSingleton<IdGenerater>();
                world.AddSingleton<CodeTypes, Assembly[]>(assemblies);
                world.AddSingleton<EntitySystemSingleton>();
            }
            catch
            {
                world?.Dispose();
                Interlocked.Exchange(ref active, 0);
                throw;
            }
        }

        public Scene CreateScene(int id, string name)
        {
            Check();
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Scene name is required.", nameof(name));
            if (fibers.ContainsKey(id)) throw new ArgumentException("Scene id is already registered.", nameof(id));
            Fiber fiber = new Fiber(id, 0, 0, name);
            fibers.Add(id, fiber);
            return fiber.Root;
        }

        public void Run(int sceneId, Action<Scene> action)
        {
            Check();
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!fibers.TryGetValue(sceneId, out Fiber fiber) || fiber.IsDisposed || fiber.Root.IsDisposed)
                throw new ArgumentException("Scene is not active.", nameof(sceneId));
            executing = true;
            try { InFiber(fiber, () => action(fiber.Root)); }
            finally { executing = false; }
        }

        public void RemoveScene(int id)
        {
            Check();
            if (!fibers.TryGetValue(id, out Fiber fiber)) return;
            executing = true;
            try { InFiber(fiber, fiber.Dispose); }
            finally { fibers.Remove(id); executing = false; }
        }

        public void Tick()
        {
            Check();
            executing = true;
            try
            {
                foreach (Fiber fiber in fibers.Values)
                {
                    if (fiber.IsDisposed || fiber.Root.IsDisposed) continue;
                    InFiber(fiber, () => { fiber.Update(); fiber.LateUpdate(); });
                }
            }
            finally { executing = false; }
        }

        private static void InFiber(Fiber fiber, Action action)
        {
            Fiber previousFiber = Fiber.Instance;
            SynchronizationContext previousContext = SynchronizationContext.Current;
            try
            {
                Fiber.Instance = fiber;
                SynchronizationContext.SetSynchronizationContext(fiber.ThreadSynchronizationContext);
                action();
            }
            finally
            {
                Fiber.Instance = previousFiber;
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        private void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(EtRuntimeHost));
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException("ET host operations require the owner thread.");
            if (executing) throw new InvalidOperationException("ET host operations cannot be reentered.");
        }

        public void Dispose()
        {
            if (disposed) return;
            Check();
            executing = true;
            try
            {
                foreach (Fiber fiber in fibers.Values) InFiber(fiber, fiber.Dispose);
            }
            finally
            {
                fibers.Clear();
                try { world.Dispose(); }
                finally
                {
                    disposed = true;
                    executing = false;
                    Interlocked.Exchange(ref active, 0);
                }
            }
        }
    }
}
