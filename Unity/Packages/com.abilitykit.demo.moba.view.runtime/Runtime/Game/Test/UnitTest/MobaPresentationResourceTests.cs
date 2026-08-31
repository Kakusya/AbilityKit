using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaPresentationResourceTests
    {
        [Serializable]
        private sealed class CharacterConfigRoot
        {
            public CharacterDTO[] Items;
        }

        [Serializable]
        private sealed class ModelConfigRoot
        {
            public ModelDTO[] Items;
        }

        [Serializable]
        private sealed class AoeConfigRoot
        {
            public AoeDTO[] Items;
        }

        [Serializable]
        private sealed class VfxConfigRoot
        {
            public VfxDTO[] Items;
        }

        [Test]
        public void HeroModels_ShouldBeAvailableThroughResources()
        {
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "moba/characters",
                root => root.Items);
            var models = LoadArray<ModelConfigRoot, ModelDTO>(
                "moba/models",
                root => root.Items);
            var modelsById = new Dictionary<int, ModelDTO>(models.Length);
            for (var i = 0; i < models.Length; i++)
            {
                var model = models[i];
                Assert.IsNotNull(model, "Model config contains a null entry at index " + i + ".");
                Assert.Greater(model.Id, 0, "Model config id must be positive at index " + i + ".");
                Assert.IsFalse(modelsById.ContainsKey(model.Id), "Duplicate model config id: " + model.Id);
                modelsById.Add(model.Id, model);
            }

            for (var i = 0; i < characters.Length; i++)
            {
                var character = characters[i];
                Assert.IsNotNull(character, "Character config contains a null entry at index " + i + ".");
                Assert.IsTrue(
                    modelsById.TryGetValue(character.ModelId, out var model),
                    "Character " + character.Id + " references missing model config: " + character.ModelId);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(model.PrefabPath),
                    "Model " + model.Id + " referenced by character " + character.Id + " has an empty prefab path.");
                Assert.IsNotNull(
                    Resources.Load<GameObject>(model.PrefabPath),
                    "Missing model resource for character " + character.Id + ": " + model.PrefabPath);
            }
        }

        [Test]
        public void AoeModels_ShouldResolveAndBeAvailableThroughResources()
        {
            var aoes = LoadArray<AoeConfigRoot, AoeDTO>("moba/aoes", root => root.Items);
            var models = LoadArray<ModelConfigRoot, ModelDTO>("moba/models", root => root.Items);
            var modelsById = new Dictionary<int, ModelDTO>(models.Length);
            for (var i = 0; i < models.Length; i++)
            {
                var model = models[i];
                Assert.IsNotNull(model, "Model config contains a null entry at index " + i + ".");
                Assert.IsTrue(modelsById.ContainsKey(model.Id) == false, "Duplicate model config id: " + model.Id);
                modelsById.Add(model.Id, model);
            }

            for (var i = 0; i < aoes.Length; i++)
            {
                var aoe = aoes[i];
                Assert.IsNotNull(aoe, "AOE config contains a null entry at index " + i + ".");
                Assert.Greater(aoe.ModelId, 0, "AOE " + aoe.Id + " must reference a positive model id.");
                Assert.IsTrue(
                    modelsById.TryGetValue(aoe.ModelId, out var model),
                    "AOE " + aoe.Id + " references missing model config: " + aoe.ModelId);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(model.PrefabPath),
                    "Model " + model.Id + " referenced by AOE " + aoe.Id + " has an empty prefab path.");
                Assert.IsNotNull(
                    Resources.Load<GameObject>(model.PrefabPath),
                    "Missing model resource for AOE " + aoe.Id + ": " + model.PrefabPath);
            }
        }

        [Test]
        public void ConfiguredEffects_ShouldBeAvailableThroughResources()
        {
            var entries = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            var ids = new HashSet<int>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                Assert.IsNotNull(entry, "VFX config contains a null entry at index " + i + ".");
                Assert.Greater(entry.Id, 0, "VFX config id must be positive at index " + i + ".");
                Assert.IsTrue(ids.Add(entry.Id), "Duplicate VFX config id: " + entry.Id);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(entry.Resource),
                    "VFX config " + entry.Id + " has an empty resource path.");
                Assert.IsNotNull(
                    Resources.Load<GameObject>(entry.Resource),
                    "Missing VFX resource for config " + entry.Id + ": " + entry.Resource);
            }
        }

        private static TItem[] LoadArray<TRoot, TItem>(
            string resourcePath,
            Func<TRoot, TItem[]> selectItems)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            Assert.IsNotNull(asset, "Missing JSON resource: " + resourcePath);

            var root = JsonUtility.FromJson<TRoot>("{\"Items\":" + asset.text + "}");
            Assert.IsNotNull(root, "Failed to parse JSON resource: " + resourcePath);
            var items = selectItems(root);
            Assert.IsNotNull(items, "JSON resource contains no array: " + resourcePath);
            Assert.IsNotEmpty(items, "JSON resource contains an empty array: " + resourcePath);
            return items;
        }

        [TestCase(1002, 10020101, 30020001, 90002011)]
        [TestCase(1004, 10040101, 30040011, 90004011)]
        [TestCase(1005, 10050101, 30050001, 90005011)]
        [TestCase(1006, 10060101, 30060001, 90006011)]
        public void RangedBasicAttackProjectiles_ShouldUseConfiguredVfx(
            int heroId,
            int activeSkillId,
            int projectileId,
            int expectedVfxId)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       skillIds: new[] { activeSkillId },
                       worldId: "presentation_resource_" + heroId,
                       heroId: heroId,
                       attributeTemplateId: heroId))
            {
                Assert.IsTrue(harness.Config.TryGetProjectile(projectileId, out var projectile), "Missing projectile config: " + projectileId);
                Assert.AreEqual(expectedVfxId, projectile.VfxId, "Basic attack projectile should reference its matching VFX.");
            }
        }
    }
}
