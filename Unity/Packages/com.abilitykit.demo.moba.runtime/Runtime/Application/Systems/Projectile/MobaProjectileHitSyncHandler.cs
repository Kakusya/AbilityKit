using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Combat.Projectile;

namespace AbilityKit.Demo.Moba.Runtime.Application.Systems.Projectile
{
    internal sealed class MobaProjectileHitSyncHandler : IProjectileSyncHandler
    {
        private readonly MobaProjectileSyncSystem _sys;

        public MobaProjectileHitSyncHandler(MobaProjectileSyncSystem sys)
        {
            _sys = sys;
        }

        public void HandleHits(List<ProjectileHitEvent> hits)
        {
            var count = hits.Count;
            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.Projectile.Value <= 0)
                {
                    Log.Warning("[MobaProjectileHitSyncHandler] Projectile id is invalid when processing hit event.");
                    continue;
                }

                _sys.ProjectileSnapshots?.RecordHit(hit);
                var hitActorId = _sys.ResolveActorIdByCollider(hit.HitCollider);
                if (hitActorId <= 0) continue;

                CollectProjectileHit(hit, hitActorId);
                _sys.StageTriggers?.ExecuteProjectileHit(hit, hitActorId);
            }

            hits.Clear();
        }

        private void CollectProjectileHit(in ProjectileHitEvent hit, int hitActorId)
        {
            var collector = _sys.EventCollector;
            if (collector == null) return;

            try
            {
                if (_sys.Links == null || !_sys.Links.TryGetSource(hit.Projectile, out var source)) return;
                var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, hitActorId, in source);
                collector.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaProjectileHitSyncHandler] diagnostic collect failed (ProjectileHit)");
            }
        }

        public void HandleSpawns(List<ProjectileSpawnEvent> spawns)
        {
        }

        public void HandleTicks(List<ProjectileTickEvent> ticks)
        {
        }

        public void HandleExits(List<ProjectileExitEvent> exits)
        {
        }
    }
}
