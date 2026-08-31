using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 球体碰撞形状
    /// </summary>
    public readonly struct Sphere : IEquatable<Sphere>
    {
        public readonly Vec3 Center;
        public readonly float Radius;

        public Sphere(in Vec3 center, float radius)
        {
            Center = center;
            Radius = MathUtil.Max(0f, radius);
        }

        public bool Equals(Sphere other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);
        public override bool Equals(object obj) => obj is Sphere other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Center, Radius);
    }

    /// <summary>
    /// 轴对齐包围盒
    /// </summary>
    public readonly struct Aabb : IEquatable<Aabb>
    {
        public readonly Vec3 Min;
        public readonly Vec3 Max;

        public Aabb(in Vec3 min, in Vec3 max)
        {
            Min = min;
            Max = max;
        }

        public Vec3 Center => (Min + Max) * 0.5f;
        public Vec3 Extents => Max - Min;

        public float SurfaceArea()
        {
            var e = Extents;
            var ex = MathUtil.Max(0f, e.X);
            var ey = MathUtil.Max(0f, e.Y);
            var ez = MathUtil.Max(0f, e.Z);
            return 2f * (ex * ey + ey * ez + ez * ex);
        }

        public bool Contains(in Vec3 point)
        {
            return point.X >= Min.X && point.X <= Max.X
                && point.Y >= Min.Y && point.Y <= Max.Y
                && point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public bool Intersects(in Aabb other)
        {
            return Min.X <= other.Max.X && Max.X >= other.Min.X
                && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
                && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }

        public Aabb Expand(float amount)
        {
            var v = new Vec3(amount, amount, amount);
            return new Aabb(Min - v, Max + v);
        }

        public bool Equals(Aabb other) => Min.Equals(other.Min) && Max.Equals(other.Max);
        public override bool Equals(object obj) => obj is Aabb other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Min, Max);
    }

    /// <summary>
    /// 胶囊体（用于角色碰撞体）
    /// </summary>
    public readonly struct Capsule : IEquatable<Capsule>
    {
        public readonly Vec3 A;
        public readonly Vec3 B;
        public readonly float Radius;

        public Capsule(in Vec3 a, in Vec3 b, float radius)
        {
            A = a;
            B = b;
            Radius = MathUtil.Max(0f, radius);
        }

        public Vec3 Center => (A + B) * 0.5f;

        public bool Equals(Capsule other) => A.Equals(other.A) && B.Equals(other.B) && Radius.Equals(other.Radius);
        public override bool Equals(object obj) => obj is Capsule other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B, Radius);
    }

    /// <summary>
    /// 有向包围盒（OBB）
    /// </summary>
    public readonly struct Obb : IEquatable<Obb>
    {
        public readonly Vec3 Center;
        public readonly Quat Rotation;
        public readonly Vec3 HalfExtents;

        public Obb(in Vec3 center, in Quat rotation, in Vec3 halfExtents)
        {
            Center = center;
            Rotation = rotation;
            HalfExtents = new Vec3(
                MathUtil.Max(0f, halfExtents.X),
                MathUtil.Max(0f, halfExtents.Y),
                MathUtil.Max(0f, halfExtents.Z));
        }

        public Vec3 Right => Rotation.Rotate(Vec3.Right);
        public Vec3 Up => Rotation.Rotate(Vec3.Up);
        public Vec3 Forward => Rotation.Rotate(Vec3.Forward);

        /// <summary>
        /// 变换到世界空间（仅平移旋转，不缩放）
        /// </summary>
        public Obb Transform(in Transform3 t)
        {
            return new Obb(t.TransformPoint(Center), t.Rotation * Rotation, HalfExtents);
        }

        /// <summary>
        /// 获取局部轴
        /// </summary>
        public void GetAxes(out Vec3 right, out Vec3 up, out Vec3 forward)
        {
            right = Right;
            up = Up;
            forward = Forward;
        }

        /// <summary>
        /// 获取 8 个角点
        /// </summary>
        public void GetCorners(out Vec3 c0, out Vec3 c1, out Vec3 c2, out Vec3 c3,
            out Vec3 c4, out Vec3 c5, out Vec3 c6, out Vec3 c7)
        {
            var r = Right * HalfExtents.X;
            var u = Up * HalfExtents.Y;
            var f = Forward * HalfExtents.Z;
            c0 = Center - r - u - f;
            c1 = Center - r - u + f;
            c2 = Center - r + u - f;
            c3 = Center - r + u + f;
            c4 = Center + r - u - f;
            c5 = Center + r - u + f;
            c6 = Center + r + u - f;
            c7 = Center + r + u + f;
        }

        public bool Equals(Obb other) => Center.Equals(other.Center) && Rotation.Equals(other.Rotation) && HalfExtents.Equals(other.HalfExtents);
        public override bool Equals(object obj) => obj is Obb other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Center, Rotation, HalfExtents);
    }

    /// <summary>
    /// 碰撞体形状类型
    /// </summary>
    public enum ColliderShapeType
    {
        Sphere = 1,
        Aabb = 2,
        Capsule = 3,
        OBB = 4
    }

    /// <summary>
    /// 碰撞体形状（联合体）
    /// </summary>
    public readonly struct ColliderShape : IEquatable<ColliderShape>
    {
        public readonly ColliderShapeType Type;
        public readonly Sphere Sphere;
        public readonly Aabb Aabb;
        public readonly Capsule Capsule;
        public readonly Obb Obb;

        private ColliderShape(ColliderShapeType type, in Sphere sphere, in Aabb aabb, in Capsule capsule, in Obb obb)
        {
            Type = type;
            Sphere = sphere;
            Aabb = aabb;
            Capsule = capsule;
            Obb = obb;
        }

        public static ColliderShape CreateSphere(in Vec3 center, float radius) => new ColliderShape(ColliderShapeType.Sphere, new Sphere(center, radius), default, default, default);
        public static ColliderShape CreateSphere(in Sphere sphere) => new ColliderShape(ColliderShapeType.Sphere, sphere, default, default, default);
        public static ColliderShape CreateAabb(in Vec3 min, in Vec3 max) => new ColliderShape(ColliderShapeType.Aabb, default, new Aabb(min, max), default, default);
        public static ColliderShape CreateCapsule(in Vec3 a, in Vec3 b, float radius) => new ColliderShape(ColliderShapeType.Capsule, default, default, new Capsule(a, b, radius), default);
        public static ColliderShape CreateObb(in Vec3 center, in Quat rotation, in Vec3 halfExtents) => new ColliderShape(ColliderShapeType.OBB, default, default, default, new Obb(center, rotation, halfExtents));

        public bool Equals(ColliderShape other) => Type == other.Type && Sphere.Equals(other.Sphere) && Aabb.Equals(other.Aabb) && Capsule.Equals(other.Capsule) && Obb.Equals(other.Obb);
        public override bool Equals(object obj) => obj is ColliderShape other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Type, Sphere, Aabb, Capsule, Obb);
    }

    /// <summary>
    /// 碰撞响应类型
    /// </summary>
    public enum CollisionResponse
    {
        Ignore = 0,
        Block = 1,
        Overlap = 2
    }

    /// <summary>
    /// 碰撞层常量
    /// </summary>
    public static class CollisionLayers
    {
        public const int Default = 0;
        public const int Player = 1;
        public const int Monster = 2;
        public const int Projectile = 3;
        public const int Building = 4;
        public const int Terrain = 5;
        public const int Destructible = 6;
        public const int Item = 7;
        public const int Count = 8;
        public const int MaxLayers = 64;
    }
}
