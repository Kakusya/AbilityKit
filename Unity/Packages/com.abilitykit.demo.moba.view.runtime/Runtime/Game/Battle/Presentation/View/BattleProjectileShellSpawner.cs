using System.Collections.Generic;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Handles projectile shell GameObject creation, destruction, and per-frame
    /// position updates through a <see cref="BattleProjectileShellPool"/>.
    /// Shells are pooled per projectileTemplateId and tracked by ProjectileActorId.
    ///
    /// Hierarchy layout (when a hierarchy manager is supplied):
    /// <c>[Battle]/_Active/_Projectile/tpl_{projectileTemplateId}/Projectile_{projectileActorId}</c>
    /// for currently-flying shells.
    ///
    /// Lifecycle:
    /// - Spawn entry → Rent shell → Track by (projectileActorId, templateId) → Position at spawn
    /// - Each tick    → Update shell positions
    /// - Exit/Hit     → Return shell to pool and remove tracking
    /// </summary>
    internal sealed class BattleProjectileShellSpawner
    {
        private readonly BattleProjectileShellPool _pool;
        private readonly BattleProjectileShellFollowResolver _followResolver;
        private readonly BattleViewHierarchyManager _hierarchy;

        /// <summary>
        /// Active shells keyed by (ProjectileActorId, templateId).
        /// Used to return the correct shell on Exit without a separate lookup table.
        /// </summary>
        private readonly Dictionary<(int actorId, int templateId), GameObject> _activeShells =
            new Dictionary<(int, int), GameObject>();

        public BattleProjectileShellSpawner(BattleProjectileShellPool pool, BattleProjectileShellFollowResolver followResolver, BattleViewHierarchyManager hierarchy = null)
        {
            _pool = pool;
            _followResolver = followResolver ?? new BattleProjectileShellFollowResolver(null);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Spawns a pooled projectile shell at the given world position and begins tracking it.
        /// </summary>
        /// <returns>The spawned shell GameObject, or null if pooling is disabled / templateId is invalid.</returns>
        public GameObject TrySpawn(int projectileTemplateId, int projectileActorId, in Vector3 position, in Vector3 forward, int launcherActorId)
        {
            if (_pool == null || projectileTemplateId <= 0) return null;

            if (!_pool.TryRent(projectileTemplateId, out var shell) || shell == null)
                return null;

            shell.name = $"Projectile_{projectileActorId}";
            shell.transform.position = position;
            shell.transform.forward = forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward;

            // Parent active shell under the categorized active root for the projectile template.
            if (_hierarchy != null)
            {
                _hierarchy.ParentActive(BattleViewCategory.ActiveProjectile, projectileTemplateId, shell);
            }

            _activeShells[(projectileActorId, projectileTemplateId)] = shell;
            return shell;
        }

        /// <summary>
        /// Finds and returns the active shell for the given projectile, if it is currently tracked.
        /// </summary>
        public bool TryFindShell(int projectileActorId, int templateId, out GameObject shell)
        {
            return _activeShells.TryGetValue((projectileActorId, templateId), out shell);
        }

        /// <summary>
        /// Stops tracking the shell for the given projectile and returns it to the pool.
        /// </summary>
        public void StopAndReturn(int projectileTemplateId, int projectileActorId)
        {
            var key = (projectileActorId, projectileTemplateId);
            if (!_activeShells.TryGetValue(key, out var shell))
                return;

            _activeShells.Remove(key);
            shell.transform.SetParent(null, worldPositionStays: false);
            _pool?.Return(projectileTemplateId, shell);
        }

        /// <summary>
        /// Per-frame tick: updates all active projectile shells to follow their launcher entities.
        /// Call once per frame from the projectile view tick.
        /// </summary>
        public void Tick()
        {
            if (_activeShells.Count == 0) return;

            var toRemove = default(List<(int, int)>);
            foreach (var kvp in _activeShells)
            {
                var (actorId, templateId) = kvp.Key;
                var shell = kvp.Value;

                if (shell == null)
                {
                    toRemove ??= new List<(int, int)>();
                    toRemove.Add(kvp.Key);
                    continue;
                }

                if (_followResolver.TryGetPosition(actorId, out var pos))
                {
                    shell.transform.position = pos;
                }
            }

            if (toRemove != null)
            {
                foreach (var k in toRemove) _activeShells.Remove(k);
            }
        }

        /// <summary>
        /// Clears all tracked shells and returns them to the pool.
        /// Call when tearing down the view layer.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _activeShells)
            {
                var (actorId, templateId) = kvp.Key;
                var shell = kvp.Value;
                if (shell != null)
                {
                    shell.transform.SetParent(null, worldPositionStays: false);
                    _pool?.Return(templateId, shell);
                }
            }
            _activeShells.Clear();
        }

        /// <summary>The number of currently tracked projectile shells.</summary>
        public int ActiveCount => _activeShells.Count;
    }

    /// <summary>
    /// Resolves the world-space position of a projectile launcher entity.
    /// </summary>
    internal sealed class BattleProjectileShellFollowResolver
    {
        private readonly IBattleEntityQuery _query;

        public BattleProjectileShellFollowResolver(IBattleEntityQuery query)
        {
            _query = query;
        }

        /// <summary>
        /// Tries to get the world-space position of the entity with the given actorId.
        /// </summary>
        public bool TryGetPosition(int actorId, out Vector3 position)
        {
            position = default;
            if (actorId <= 0 || _query == null) return false;

            var netId = new BattleNetId(actorId);
            if (_query.TryGetTransform(netId, out var tr) && tr != null)
            {
                position = tr.Position;
                return true;
            }
            return false;
        }
    }
}
