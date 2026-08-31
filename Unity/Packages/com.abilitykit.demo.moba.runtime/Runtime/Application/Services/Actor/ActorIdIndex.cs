using System;
using System.Collections.Generic;
using Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(ActorIdIndex))]
    public sealed class ActorIdIndex : IService
    {
        private readonly Dictionary<int, global::ActorEntity> _map = new Dictionary<int, global::ActorEntity>();
        private readonly IGroup<global::ActorEntity> _group;

        // 处理 ReplaceActorId 组件替换回调。
        private readonly Dictionary<global::ActorEntity, EntityComponentReplaced> _replaceHandlers
            = new Dictionary<global::ActorEntity, EntityComponentReplaced>();

        public ActorIdIndex(global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));

            var ctx = (global::Contexts)contexts;
            _group = ctx.actor.GetGroup(global::ActorMatcher.ActorId);
            _group.OnEntityAdded += OnEntityAdded;
            _group.OnEntityRemoved += OnEntityRemoved;

            var existing = _group.GetEntities();
            for (int i = 0; i < existing.Length; i++)
            {
                Track(existing[i]);
            }
        }

        public bool TryGet(int actorId, out global::ActorEntity entity)
        {
            if (actorId <= 0)
            {
                entity = null;
                return false;
            }

            if (_map.TryGetValue(actorId, out entity) && entity != null && entity.isEnabled)
            {
                return true;
            }

            entity = null;
            return false;
        }

        private void OnEntityAdded(IGroup<global::ActorEntity> group, global::ActorEntity entity, int index, IComponent component)
        {
            Track(entity);
        }

        private void OnEntityRemoved(IGroup<global::ActorEntity> group, global::ActorEntity entity, int index, IComponent component)
        {
            Untrack(entity);
        }

        private void Track(global::ActorEntity entity)
        {
            if (entity == null || !entity.isEnabled || !entity.hasActorId) return;

            var actorId = entity.actorId.Value;
            if (actorId <= 0) return;

            _map[actorId] = entity;

            if (_replaceHandlers.ContainsKey(entity)) return;

            EntityComponentReplaced handler = (e, componentIndex, previousComponent, newComponent) =>
            {
                if (componentIndex != ActorComponentsLookup.ActorId) return;

                if (previousComponent is AbilityKit.Demo.Moba.Components.ActorIdComponent prev)
                {
                    if (prev.Value > 0) _map.Remove(prev.Value);
                }

                if (newComponent is AbilityKit.Demo.Moba.Components.ActorIdComponent cur)
                {
                    var actorEntity = (global::ActorEntity)e;
                    if (cur.Value > 0 && actorEntity.isEnabled) _map[cur.Value] = actorEntity;
                }
            };

            entity.OnComponentReplaced += handler;
            _replaceHandlers[entity] = handler;
        }

        private void Untrack(global::ActorEntity entity)
        {
            if (entity == null) return;

            if (entity.hasActorId)
            {
                var actorId = entity.actorId.Value;
                if (actorId > 0) _map.Remove(actorId);
            }

            if (_replaceHandlers.TryGetValue(entity, out var handler))
            {
                entity.OnComponentReplaced -= handler;
                _replaceHandlers.Remove(entity);
            }
        }

        public void Dispose()
        {
            _group.OnEntityAdded -= OnEntityAdded;
            _group.OnEntityRemoved -= OnEntityRemoved;

            foreach (var kv in _replaceHandlers)
            {
                if (kv.Key != null) kv.Key.OnComponentReplaced -= kv.Value;
            }
            _replaceHandlers.Clear();
            _map.Clear();
        }
    }
}

