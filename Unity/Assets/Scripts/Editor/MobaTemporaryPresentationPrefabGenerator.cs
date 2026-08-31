#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Tools
{
    public static class MobaTemporaryPresentationPrefabGenerator
    {
        private const string ResourceRoot = "Packages/com.abilitykit.demo.moba.view.runtime/Resources";
        private const string MaterialRoot = ResourceRoot + "/generated_materials";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Tools/AbilityKit/MOBA/Generate Temporary Presentation Prefabs")]
        public static void GenerateAll()
        {
            Materials.Clear();
            EnsureFolder(ResourceRoot + "/character");
            EnsureFolder(ResourceRoot + "/effect");
            EnsureFolder(MaterialRoot);

            CreateZhaoYun();
            CreateMozi();
            CreateDaji();
            CreateYingZheng();
            CreateSunShangXiang();
            CreateLianPoSkill2Circle();
            CreateXiaoQiaoSkill2Lift();
            CreateXiaoQiaoBasicFan();
            CreateMoziBasicEnergyOrb();
            CreateDajiBasicSoulOrb();
            CreateYingZhengBasicSword();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated temporary MOBA presentation prefabs in the MOBA view-runtime package.");
        }

        private static void CreateZhaoYun()
        {
            var root = CreateRoot("ZhaoYunTemporary", "character/character3");
            AddPrimitive(root, PrimitiveType.Capsule, "ArmoredBody", new Vector3(0f, 1f, 0f), new Vector3(0.72f, 1.1f, 0.5f), ColorFor("zhaoyun_armor", new Color(0.12f, 0.33f, 0.78f)));
            AddPrimitive(root, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 2.05f, 0f), new Vector3(0.62f, 0.62f, 0.62f), ColorFor("zhaoyun_gold", new Color(0.95f, 0.7f, 0.16f)));
            AddPrimitive(root, PrimitiveType.Cylinder, "Spear", new Vector3(0.65f, 1.15f, 0f), new Vector3(0.07f, 1.25f, 0.07f), ColorFor("zhaoyun_spear", new Color(0.78f, 0.84f, 0.9f)), new Vector3(0f, 0f, 72f));
            Save(root, "character/character3");
        }

        private static void CreateMozi()
        {
            var root = CreateRoot("MoziTemporary", "character/character4");
            AddPrimitive(root, PrimitiveType.Cube, "MechBody", new Vector3(0f, 1.05f, 0f), new Vector3(0.95f, 1.25f, 0.62f), ColorFor("mozi_body", new Color(0.12f, 0.48f, 0.66f)));
            AddPrimitive(root, PrimitiveType.Sphere, "PowerCore", new Vector3(0f, 1.12f, -0.34f), new Vector3(0.32f, 0.32f, 0.16f), ColorFor("mozi_core", new Color(0.24f, 0.92f, 1f)));
            AddPrimitive(root, PrimitiveType.Cylinder, "ShoulderCannon", new Vector3(0.58f, 1.55f, 0f), new Vector3(0.23f, 0.55f, 0.23f), ColorFor("mozi_cannon", new Color(0.5f, 0.58f, 0.64f)), new Vector3(0f, 0f, 90f));
            Save(root, "character/character4");
        }

        private static void CreateDaji()
        {
            var root = CreateRoot("DajiTemporary", "character/character5");
            AddPrimitive(root, PrimitiveType.Capsule, "MageBody", new Vector3(0f, 1f, 0f), new Vector3(0.62f, 1.08f, 0.45f), ColorFor("daji_dress", new Color(0.72f, 0.2f, 0.56f)));
            AddPrimitive(root, PrimitiveType.Sphere, "FoxHead", new Vector3(0f, 2.02f, 0f), new Vector3(0.55f, 0.55f, 0.55f), ColorFor("daji_hair", new Color(0.98f, 0.55f, 0.78f)));
            AddPrimitive(root, PrimitiveType.Capsule, "TailLeft", new Vector3(-0.36f, 0.7f, 0.33f), new Vector3(0.17f, 0.62f, 0.17f), ColorFor("daji_tail", new Color(0.98f, 0.62f, 0.82f)), new Vector3(28f, 0f, 25f));
            AddPrimitive(root, PrimitiveType.Capsule, "TailRight", new Vector3(0.36f, 0.7f, 0.33f), new Vector3(0.17f, 0.62f, 0.17f), ColorFor("daji_tail", new Color(0.98f, 0.62f, 0.82f)), new Vector3(28f, 0f, -25f));
            Save(root, "character/character5");
        }

        private static void CreateYingZheng()
        {
            var root = CreateRoot("YingZhengTemporary", "character/character6");
            AddPrimitive(root, PrimitiveType.Capsule, "RoyalBody", new Vector3(0f, 1.05f, 0f), new Vector3(0.7f, 1.18f, 0.5f), ColorFor("yingzheng_robe", new Color(0.2f, 0.18f, 0.38f)));
            AddPrimitive(root, PrimitiveType.Cylinder, "Crown", new Vector3(0f, 2.2f, 0f), new Vector3(0.34f, 0.12f, 0.34f), ColorFor("yingzheng_gold", new Color(1f, 0.75f, 0.18f)));
            AddPrimitive(root, PrimitiveType.Cube, "HoverSword", new Vector3(0.68f, 1.25f, 0f), new Vector3(0.12f, 0.75f, 0.18f), ColorFor("yingzheng_sword", new Color(0.95f, 0.82f, 0.32f)), new Vector3(0f, 0f, -28f));
            Save(root, "character/character6");
        }

        private static void CreateSunShangXiang()
        {
            var root = CreateRoot("SunShangXiangTemporary", "character/character7");
            AddPrimitive(root, PrimitiveType.Capsule, "FighterBody", new Vector3(0f, 1f, 0f), new Vector3(0.65f, 1.08f, 0.46f), ColorFor("sun_red", new Color(0.86f, 0.22f, 0.2f)));
            AddPrimitive(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.02f, 0f), new Vector3(0.54f, 0.54f, 0.54f), ColorFor("sun_hair", new Color(0.98f, 0.55f, 0.2f)));
            AddPrimitive(root, PrimitiveType.Cylinder, "Cannon", new Vector3(0.65f, 1.12f, 0f), new Vector3(0.24f, 0.62f, 0.24f), ColorFor("sun_cannon", new Color(0.42f, 0.46f, 0.55f)), new Vector3(0f, 0f, 90f));
            Save(root, "character/character7");
        }

        private static void CreateLianPoSkill2Circle()
        {
            var root = CreateRoot("LianPoSkill2CircleTemporary", "effect/lianpo_skill2_circle");
            AddPrimitive(root, PrimitiveType.Cylinder, "ImpactDisk", Vector3.zero, new Vector3(2.7f, 0.04f, 2.7f), ColorFor("lianpo_impact", new Color(0.92f, 0.38f, 0.12f)));
            AddRing(root, "OuterRing", Vector3.up * 0.08f, 1.35f, 0.12f, ColorFor("lianpo_ring", new Color(1f, 0.76f, 0.24f)));
            Save(root, "effect/lianpo_skill2_circle");
        }

        private static void CreateXiaoQiaoSkill2Lift()
        {
            var root = CreateRoot("XiaoQiaoSkill2LiftTemporary", "effect/xiaoqiao_skill2_lift");
            AddPrimitive(root, PrimitiveType.Cylinder, "LiftColumn", new Vector3(0f, 0.9f, 0f), new Vector3(0.62f, 0.9f, 0.62f), ColorFor("xiaoqiao_lift", new Color(0.98f, 0.42f, 0.78f)));
            var ringMaterial = ColorFor("xiaoqiao_ring", new Color(1f, 0.82f, 0.94f));
            AddRing(root, "LowerRing", new Vector3(0f, 0.1f, 0f), 0.58f, 0.09f, ringMaterial);
            AddRing(root, "UpperRing", new Vector3(0f, 1.7f, 0f), 0.46f, 0.07f, ringMaterial);
            Save(root, "effect/xiaoqiao_skill2_lift");
        }

        private static void CreateXiaoQiaoBasicFan()
        {
            var root = CreateRoot("XiaoQiaoBasicFanTemporary", "effect/xiaoqiao_basic_fan");
            var material = ColorFor("xiaoqiao_fan", new Color(1f, 0.55f, 0.8f));
            AddPrimitive(root, PrimitiveType.Sphere, "FanCore", Vector3.zero, new Vector3(0.3f, 0.08f, 0.3f), material);
            for (var i = 0; i < 5; i++) AddPrimitive(root, PrimitiveType.Cube, "FanBlade" + i, Vector3.zero, new Vector3(0.78f, 0.07f, 0.2f), material, new Vector3(0f, i * 36f, 0f));
            Save(root, "effect/xiaoqiao_basic_fan");
        }

        private static void CreateMoziBasicEnergyOrb()
        {
            var root = CreateRoot("MoziBasicEnergyOrbTemporary", "effect/mozi_basic_energy_orb");
            AddPrimitive(root, PrimitiveType.Sphere, "EnergyCore", Vector3.zero, Vector3.one * 0.42f, ColorFor("mozi_orb", new Color(0.12f, 0.83f, 1f)));
            AddRing(root, "EnergyRing", Vector3.zero, 0.3f, 0.06f, ColorFor("mozi_ring", new Color(0.6f, 0.96f, 1f)));
            Save(root, "effect/mozi_basic_energy_orb");
        }

        private static void CreateDajiBasicSoulOrb()
        {
            var root = CreateRoot("DajiBasicSoulOrbTemporary", "effect/daji_basic_soul_orb");
            AddPrimitive(root, PrimitiveType.Sphere, "SoulCore", Vector3.zero, Vector3.one * 0.38f, ColorFor("daji_orb", new Color(0.96f, 0.3f, 0.77f)));
            AddPrimitive(root, PrimitiveType.Sphere, "SoulTrail", new Vector3(0f, 0f, -0.32f), new Vector3(0.2f, 0.2f, 0.42f), ColorFor("daji_trail", new Color(0.72f, 0.24f, 0.88f)));
            Save(root, "effect/daji_basic_soul_orb");
        }

        private static void CreateYingZhengBasicSword()
        {
            var root = CreateRoot("YingZhengBasicSwordTemporary", "effect/yingzheng_basic_sword");
            AddPrimitive(root, PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0.25f), new Vector3(0.13f, 0.16f, 1.1f), ColorFor("yingzheng_blade", new Color(1f, 0.86f, 0.34f)));
            AddPrimitive(root, PrimitiveType.Cube, "Guard", new Vector3(0f, 0f, -0.18f), new Vector3(0.5f, 0.13f, 0.1f), ColorFor("yingzheng_guard", new Color(0.95f, 0.65f, 0.12f)));
            Save(root, "effect/yingzheng_basic_sword");
        }

        private static GameObject CreateRoot(string name, string resourcePath)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;
            return root;
        }

        private static void AddPrimitive(GameObject root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = rotation;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void AddRing(GameObject root, string name, Vector3 center, float radius, float thickness, Material material)
        {
            const int segmentCount = 8;
            for (var i = 0; i < segmentCount; i++)
            {
                var angle = i * Mathf.PI * 2f / segmentCount;
                var position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                AddPrimitive(
                    root,
                    PrimitiveType.Cube,
                    name + "Segment" + i,
                    position,
                    new Vector3(radius * 0.75f, thickness, thickness),
                    material,
                    new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
            }
        }

        private static Material ColorFor(string key, Color color)
        {
            if (Materials.TryGetValue(key, out var material)) return material;

            var assetPath = MaterialRoot + "/" + key + ".mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                material.name = key;
                material.color = color;
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Materials.Add(key, material);
            return material;
        }

        private static void Save(GameObject root, string resourcePath)
        {
            var path = ResourceRoot + "/" + resourcePath + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
#endif
