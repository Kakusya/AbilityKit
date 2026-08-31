using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleAreaViewHandle
    {
        public int AreaId { get; set; }
        public int TemplateId { get; set; }
        public GameObject ModelGo { get; set; }
        public GameObject VfxGo { get; set; }
        public GameObject RangeGo { get; set; }

        /// <summary>
        /// Pool to return objects to. Set by the factory before use.
        /// </summary>
        internal BattleAreaVfxPool Pool { get; set; }

        public void Destroy()
        {
            ReturnOrDestroy(Pool, TemplateId, BattleAreaVfxPool.PoolKind.Model, ModelGo);
            ModelGo = null;

            ReturnOrDestroy(Pool, TemplateId, BattleAreaVfxPool.PoolKind.Range, RangeGo);
            RangeGo = null;

            ReturnOrDestroy(Pool, TemplateId, BattleAreaVfxPool.PoolKind.Vfx, VfxGo);
            VfxGo = null;

            Pool = null;
        }

        private static void ReturnOrDestroy(BattleAreaVfxPool pool, int templateId, BattleAreaVfxPool.PoolKind kind, GameObject go)
        {
            if (go == null) return;

            if (pool != null && templateId > 0 && pool.TryReturn(templateId, kind, go))
                return;

            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
