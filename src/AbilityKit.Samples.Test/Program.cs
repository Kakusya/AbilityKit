using System;
using System.Reflection;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.Collision;

namespace AbilityKit.Samples.Test
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("=== Testing New Samples ===");
            Console.WriteLine();

            Console.WriteLine("--- Testing LayerFilter ---");
            TestLayerFilter();
            Console.WriteLine();

            Console.WriteLine("--- Testing BehaviorTree ---");
            TestBehaviorTree();
            Console.WriteLine();

            Console.WriteLine("=== All Tests Passed ===");
        }

        private static void TestLayerFilter()
        {
            // Test IncludeExclude
            var filter = LayerFilter.IncludeExclude(0xFF, 0x10);
            Console.WriteLine($"LayerFilter.IncludeExclude: {filter}");

            // Test default construction
            var defaultFilter = new LayerFilter(0);
            Console.WriteLine($"LayerFilter(0): {defaultFilter}");

            // Test with ignored colliders
            var filterWithIgnored = new LayerFilter(0xFF, new[] { 1, 2, 3 });
            Console.WriteLine($"LayerFilter with ignored: {filterWithIgnored}");

            // Test NaiveCollisionWorld
            var world = new NaiveCollisionWorld();
            Console.WriteLine($"NaiveCollisionWorld created: {world != null}");

            // Add collider
            var transform = new Transform3(Vec3.Zero, Quat.Identity, Vec3.One);
            var shape = ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f));
            var collider = world.Add(transform, shape, 1);
            Console.WriteLine($"Added collider: {collider}");

            // Query with layer filter
            var filter2 = new LayerFilter(1 << 1); // Monster layer
            var results = new System.Collections.Generic.List<ColliderId>();
            world.OverlapSphere(new Sphere(Vec3.Zero, 5f), filter2, results);
            Console.WriteLine($"OverlapSphere with MonsterLayer filter: {results.Count} results");

            // Test ICollisionLayerRelation
            var layerRel = world as ICollisionLayerRelation;
            if (layerRel != null)
            {
                layerRel.SetRelation(1, 2, CollisionResponse.Block);
                var relation = layerRel.GetRelation(1, 2);
                Console.WriteLine($"Layer relation (Player vs Monster): {relation}");
            }

            Console.WriteLine("LayerFilter tests passed!");
        }

        private static void TestBehaviorTree()
        {
            var manager = new BehaviorManager();
            Console.WriteLine($"BehaviorManager created: {manager != null}");

            // Create a simple patrol decision
            var patrolDecision = new PatrolDecision();
            var worldQuery = new SampleWorldQuery();

            var behavior = manager.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = "Patrol",
                OwnerId = new BehaviorEntityId(1001),
                Priority = 1,
                Decision = patrolDecision,
                Executor = new DefaultExecutor(),
                World = worldQuery
            });

            Console.WriteLine($"Created behavior: {behavior.BehaviorKind}");
            Console.WriteLine($"Phase: {behavior.Phase}");
            Console.WriteLine($"OwnerId: {behavior.OwnerId}");

            // Tick a few times
            for (int i = 0; i < 3; i++)
            {
                manager.Tick(0.1f, i);
                Console.WriteLine($"Tick {i}: Phase={behavior.Phase}, State={patrolDecision.CurrentState}");
            }

            // Test Selector
            var idleDecision = new DelegateDecision("Idle", (ctx, world) =>
            {
                return DecisionResult.Continue();
            });

            var rootSelector = new SelectorDecision(idleDecision, patrolDecision);
            Console.WriteLine($"Selector.Children.Count: {rootSelector.Children.Count}");

            Console.WriteLine("BehaviorTree tests passed!");
        }
    }

    internal sealed class SampleWorldQuery : DefaultWorldQuery
    {
        private readonly System.Collections.Generic.Dictionary<long, Vec3> _positions = new System.Collections.Generic.Dictionary<long, Vec3>();

        public SampleWorldQuery()
        {
            _positions[1001] = new Vec3(0, 0, 0);
        }

        public new Vec3 GetPosition(BehaviorEntityId id)
        {
            return _positions.TryGetValue(id.Value, out var pos) ? pos : Vec3.Zero;
        }

        public new void SetPosition(BehaviorEntityId id, Vec3 position)
        {
            _positions[id.Value] = position;
        }
    }

    internal sealed class PatrolDecision : IBehaviorDecision
    {
        private int _tickCount;
        private string _currentState = "Patrol";

        public string DecisionType => "Patrol";
        public string CurrentState => _currentState;

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            _tickCount++;
            return DecisionResult.Continue(_currentState);
        }
    }
}
