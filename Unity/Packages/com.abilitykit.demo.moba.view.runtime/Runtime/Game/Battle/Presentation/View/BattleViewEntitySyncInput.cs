using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleViewEntitySyncInput
    {
        internal BattleViewEntitySyncInput(
            EC.IEntity entity,
            int actorId,
            BattleTransformComponent transform,
            BattleEntityMetaComponent meta)
        {
            Entity = entity;
            ActorId = actorId;
            Transform = transform;
            Meta = meta;
        }

        public EC.IEntity Entity { get; }

        public int ActorId { get; }

        public BattleTransformComponent Transform { get; }

        public BattleEntityMetaComponent Meta { get; }
    }

    internal sealed class BattleViewEntitySyncInputFactory
    {
        public bool TryCreate(EC.IEntity entity, out BattleViewEntitySyncInput input)
        {
            input = default;

            if (!entity.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return false;
            if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return false;

            var actorId = netIdComp.NetId.Value;
            if (actorId <= 0) return false;

            var meta = entity.TryGetRef(out BattleEntityMetaComponent metaComp) ? metaComp : null;

            // Projectile and AreaEffect entities use their own view systems (not shell binding).
            if (meta != null && !BattleViewConfigLookup.UsesShellBinding(meta.Kind)) return false;

            input = new BattleViewEntitySyncInput(entity, actorId, transform, meta);
            return true;
        }
    }
}
