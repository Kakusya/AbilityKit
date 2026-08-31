using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Battle.Vfx
{
    internal sealed class BattleVfxFollowController
    {
        private readonly BattleVfxFollowTargetPositionResolver _targetPositions;
        private readonly BattleVfxLifetimePolicy _lifetime;
        private readonly BattleVfxEntityCollector _collector;
        private readonly List<EC.IEntityId> _ids;

        public BattleVfxFollowController(
            BattleVfxLifetimePolicy lifetime = null,
            BattleVfxFollowTargetPositionResolver targetPositions = null,
            BattleVfxEntityCollector collector = null,
            BattleVfxFollowControllerFactory factory = null)
        {
            factory ??= new BattleVfxFollowControllerFactory();

            _lifetime = lifetime ?? factory.CreateLifetimePolicy();
            _targetPositions = targetPositions ?? factory.CreateTargetPositionResolver();
            _collector = collector ?? factory.CreateCollector();
            _ids = factory.CreateIdBuffer();
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder, Action<EC.IECWorld, EC.IEntityId> destroyAction)
        {
            Tick(vfxRoot, binder, query: null, destroyAction);
        }

        public void Tick(in EC.IEntity vfxRoot, BattleViewBinder binder, IBattleEntityQuery query, Action<EC.IECWorld, EC.IEntityId> destroyAction)
        {
            if (!vfxRoot.IsValid) return;
            var world = vfxRoot.World;
            if (world == null) return;
            if (destroyAction == null) return;

            _ids.Clear();
            _collector.Collect(vfxRoot, _ids);
            if (_ids.Count == 0) return;

            for (int i = 0; i < _ids.Count; i++)
            {
                var id = _ids[i];
                if (!world.IsAlive(id)) continue;

                var entity = world.Wrap(id);
                if (_lifetime.IsExpired(entity))
                {
                    destroyAction(world, id);
                    continue;
                }

                TrySyncFollow(world, entity, binder, query);
            }
        }

        public int DestroyByFollowTargetActorId(in EC.IEntity vfxRoot, int targetActorId, Action<EC.IECWorld, EC.IEntityId> destroyAction)
        {
            if (targetActorId <= 0) return 0;
            if (!vfxRoot.IsValid) return 0;
            var world = vfxRoot.World;
            if (world == null) return 0;
            if (destroyAction == null) return 0;

            _ids.Clear();
            _collector.Collect(vfxRoot, _ids);
            if (_ids.Count == 0) return 0;

            var destroyed = 0;
            for (int i = 0; i < _ids.Count; i++)
            {
                var id = _ids[i];
                if (!world.IsAlive(id)) continue;

                var entity = world.Wrap(id);
                if (!entity.TryGetRef(out BattleViewFollowComponent follow) || follow == null) continue;
                if (follow.TargetActorId != targetActorId) continue;

                destroyAction(world, id);
                destroyed++;
            }

            return destroyed;
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos)
        {
            var forward = Vector3.zero;
            SyncFollow(world, vfxEntityId, in targetPos, in forward);
        }

        public void SyncFollow(EC.IECWorld world, EC.IEntityId vfxEntityId, in Vector3 targetPos, in Vector3 targetForward)
        {
            if (world == null) return;
            if (!world.IsAlive(vfxEntityId)) return;

            var entity = world.Wrap(vfxEntityId);
            if (!entity.TryGetRef(out BattleViewGameObjectComponent goComp) || goComp == null || goComp.GameObject == null) return;

            var pos = targetPos;
            if (entity.TryGetRef(out BattleViewFollowComponent follow) && follow != null)
            {
                pos += follow.Offset;
            }

            var tr = goComp.GameObject.transform;
            tr.position = pos;
            if (targetForward.sqrMagnitude > 0.0001f)
            {
                tr.rotation = Quaternion.LookRotation(targetForward.normalized, Vector3.up);
            }
        }

        private void TrySyncFollow(EC.IECWorld world, EC.IEntity entity, BattleViewBinder binder, IBattleEntityQuery query)
        {
            if (_targetPositions.TryResolve(world, entity, binder, query, out var position, out var forward))
            {
                SyncFollow(world, entity.Id, in position, in forward);
            }
        }
    }

    internal sealed class BattleVfxFollowControllerFactory
    {
        public BattleVfxLifetimePolicy CreateLifetimePolicy()
        {
            return new BattleVfxLifetimePolicy();
        }

        public BattleVfxFollowTargetPositionResolver CreateTargetPositionResolver()
        {
            return new BattleVfxFollowTargetPositionResolver();
        }

        public BattleVfxEntityCollector CreateCollector()
        {
            return new BattleVfxEntityCollector();
        }

        public List<EC.IEntityId> CreateIdBuffer()
        {
            return new List<EC.IEntityId>(32);
        }
    }
}
