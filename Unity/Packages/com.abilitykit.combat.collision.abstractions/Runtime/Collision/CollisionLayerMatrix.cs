using System;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 碰撞层关系矩阵
    /// 提供 O(1) 层间关系查询
    /// 对称矩阵，64x64 存储
    /// </summary>
    public sealed class CollisionLayerMatrix
    {
        private readonly Core.Mathematics.CollisionResponse[] _matrix;

        public CollisionLayerMatrix()
        {
            _matrix = new Core.Mathematics.CollisionResponse[64 * 64];
            SetDefaultRelations();
        }

        private void SetDefaultRelations()
        {
            for (var i = 0; i < 64; i++)
            {
                for (var j = 0; j < 64; j++)
                {
                    var index = i * 64 + j;
                    _matrix[index] = (i == j) ? Core.Mathematics.CollisionResponse.Ignore : Core.Mathematics.CollisionResponse.Block;
                }
            }
            SetRelation(2, 2, Core.Mathematics.CollisionResponse.Overlap); // Monster vs Monster
        }

        public void SetRelation(int layerA, int layerB, Core.Mathematics.CollisionResponse response)
        {
            if (layerA < 0 || layerA >= 64 || layerB < 0 || layerB >= 64) return;
            _matrix[layerA * 64 + layerB] = response;
            _matrix[layerB * 64 + layerA] = response;
        }

        public Core.Mathematics.CollisionResponse GetRelation(int layerA, int layerB)
        {
            if (layerA < 0 || layerA >= 64 || layerB < 0 || layerB >= 64) return Core.Mathematics.CollisionResponse.Ignore;
            return _matrix[layerA * 64 + layerB];
        }

        public bool ShouldDetect(int layerA, int layerB) => GetRelation(layerA, layerB) != Core.Mathematics.CollisionResponse.Ignore;
        public bool ShouldBlock(int layerA, int layerB) => GetRelation(layerA, layerB) == Core.Mathematics.CollisionResponse.Block;
        public bool ShouldOverlap(int layerA, int layerB)
        {
            var r = GetRelation(layerA, layerB);
            return r == Core.Mathematics.CollisionResponse.Block || r == Core.Mathematics.CollisionResponse.Overlap;
        }
    }
}
