using AbilityKit.Game.Flow.Battle.ViewEvents;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewEventSinkFactory
    {
        public IBattleViewEventSink Create(IViewFeatureRuntime runtime)
        {
            if (runtime == null) return null;

            return new BattleViewEventSink(
                runtime.Context,
                runtime.Query,
                runtime.Binder,
                runtime.Vfx,
                runtime.VfxNode,
                runtime.FloatingTexts,
                runtime.AreaViews,
                runtime.Resources,
                handlers: null,
                hierarchy: runtime.Hierarchy);
        }
    }
}
