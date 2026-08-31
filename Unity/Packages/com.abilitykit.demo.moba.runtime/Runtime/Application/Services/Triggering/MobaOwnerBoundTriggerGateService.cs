using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services.Passive;

namespace AbilityKit.Demo.Moba.Services.Triggering
{
    /// <summary>
    /// 聚合 owner-bound 触发器门控，让触发订阅链路不直接依赖具体玩法模块。
    /// </summary>
    [WorldService(typeof(MobaOwnerBoundTriggerGateService))]
    public sealed class MobaOwnerBoundTriggerGateService : IService
    {
        [WorldInject(required: false)] private MobaPassiveSkillLifecycleService _passiveGate = null;

        private readonly List<IMobaOwnerBoundTriggerGate> _runtimeGates = new List<IMobaOwnerBoundTriggerGate>(4);
        private readonly Stack<MobaOwnerBoundTriggerExecutionSource> _evaluationSources = new Stack<MobaOwnerBoundTriggerExecutionSource>();

        public void RegisterGate(IMobaOwnerBoundTriggerGate gate)
        {
            if (gate == null) throw new ArgumentNullException(nameof(gate));
            if (_runtimeGates.Contains(gate)) return;
            _runtimeGates.Add(gate);
        }

        public bool UnregisterGate(IMobaOwnerBoundTriggerGate gate)
        {
            return gate != null && _runtimeGates.Remove(gate);
        }

        public bool HasGate(long ownerKey, int triggerId)
        {
            return TryGetGate(ownerKey, triggerId, out _);
        }

        public bool CanExecute(long ownerKey, int triggerId)
        {
            if (!TryGetGate(ownerKey, triggerId, out var gate)) return true;
            return gate.CanExecute(ownerKey, triggerId);
        }

        public void Complete(long ownerKey, int triggerId)
        {
            if (!TryGetGate(ownerKey, triggerId, out var gate)) return;
            gate.Complete(ownerKey, triggerId);
        }

        public bool TryGetExecutionSource(long ownerKey, int triggerId, out MobaOwnerBoundTriggerExecutionSource source)
        {
            source = default;
            if (!TryGetGate(ownerKey, triggerId, out var gate)) return false;
            if (!(gate is IMobaOwnerBoundTriggerExecutionSourceProvider provider)) return false;

            return provider.TryGetExecutionSource(ownerKey, triggerId, out source) && source.HasExecutionSource;
        }

        public IDisposable BeginEvaluationScope(in MobaOwnerBoundTriggerExecutionSource source)
        {
            if (!source.HasExecutionSource) return EmptyScope.Instance;
            _evaluationSources.Push(source);
            return new EvaluationScope(this);
        }

        public bool TryGetCurrentEvaluationSource(out MobaOwnerBoundTriggerExecutionSource source)
        {
            if (_evaluationSources.Count > 0)
            {
                source = _evaluationSources.Peek();
                return source.HasExecutionSource;
            }

            source = default;
            return false;
        }

        public void Dispose()
        {
            _runtimeGates.Clear();
            _evaluationSources.Clear();
        }

        private void EndEvaluationScope()
        {
            if (_evaluationSources.Count > 0) _evaluationSources.Pop();
        }

        private bool TryGetGate(long ownerKey, int triggerId, out IMobaOwnerBoundTriggerGate gate)
        {
            gate = null;
            if (ownerKey == 0 || triggerId <= 0) return false;

            if (_passiveGate != null && _passiveGate.IsMatch(ownerKey, triggerId))
            {
                gate = _passiveGate;
                return true;
            }

            for (int i = 0; i < _runtimeGates.Count; i++)
            {
                var candidate = _runtimeGates[i];
                if (candidate == null) continue;

                try
                {
                    if (!candidate.IsMatch(ownerKey, triggerId)) continue;
                    gate = candidate;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaOwnerBoundTriggerGateService] gate match failed. ownerKey={ownerKey} triggerId={triggerId}");
                }
            }

            return false;
        }

        private sealed class EvaluationScope : IDisposable
        {
            private MobaOwnerBoundTriggerGateService _owner;

            public EvaluationScope(MobaOwnerBoundTriggerGateService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                owner?.EndEvaluationScope();
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static readonly EmptyScope Instance = new EmptyScope();

            public void Dispose()
            {
            }
        }
    }
}
