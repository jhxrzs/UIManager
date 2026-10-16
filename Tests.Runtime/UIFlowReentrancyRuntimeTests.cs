using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public class UIFlowReentrancyRuntimeTests
    {
        private GameObject canvasRoot;
        private readonly List<Material> materials = new();

        [SetUp]
        public void SetUp()
        {
            canvasRoot = new GameObject("ReentrantFlowCanvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasRoot) Object.Destroy(canvasRoot);
            yield return null;
            foreach (var material in materials) if (material) Object.Destroy(material);
            materials.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CurrentMember_ReparentDuringMaterialInstall_ShouldKeepDestinationMaterial()
        {
            var source = CreateGroup("Source", out var sourceContent);
            var destination = CreateGroup("Destination", out var destinationContent);
            var image = CreateMember(sourceContent, out var effect);
            yield return null;
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            var sourceMaterial = CreateMaterial("SourceInstall");
            var destinationMaterial = CreateMaterial("DestinationInstall");
            SetTemplate(source, sourceMaterial);
            SetTemplate(destination, destinationMaterial);
            image.material = null;

            var moved = false;
            Material installedAtCallback = null;
            UnityAction moveCurrentMember = () =>
            {
                if (moved) return;
                moved = true;
                installedAtCallback = image.material;
                image.transform.SetParent(destinationContent, false);
                TraceMember("install callback after reparent", destinationUI, image, effect);
            };
            image.RegisterDirtyMaterialCallback(moveCurrentMember);
            try
            {
                sourceUI.Hide();
                TraceMember("after interrupted source install", sourceUI, image, effect);
                Assert.IsTrue(moved, "The material setter must invoke its actual dirty callback.");
                Assert.AreSame(sourceMaterial, installedAtCallback,
                    "The callback must occur inside installation of the source shared material.");
                Assert.AreSame(destinationContent, image.transform.parent);
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                Assert.AreSame(destinationMaterial, effect.materialPrefab);
                Assert.AreSame(destinationMaterial, image.material,
                    "A material installed by the old flow effect must not be mistaken for a user override.");
            }
            finally { image.UnregisterDirtyMaterialCallback(moveCurrentMember); }
        }

        [UnityTest]
        public IEnumerator CurrentMember_DestroyEffectDuringMaterialInstall_ShouldRestoreMaterialAndFinish()
        {
            var group = CreateGroup("DestroyCurrentEffect", out var content);
            var image = CreateMember(content, out var effect);
            yield return null;
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            var sharedMaterial = CreateMaterial("DestroyedEffectInstall");
            SetTemplate(group, sharedMaterial);
            image.material = null;

            var destroyed = false;
            Material materialAfterDestroy = null;
            UnityAction destroyCurrentEffect = () =>
            {
                if (destroyed) return;
                destroyed = true;
                Object.DestroyImmediate(effect);
                materialAfterDestroy = image.material;
                TraceMember("immediately after effect removal", ui, image, effect);
            };
            image.RegisterDirtyMaterialCallback(destroyCurrentEffect);
            Exception failure = null;
            try
            {
                try { ui.Hide(); }
                catch (Exception exception) { failure = exception; }
            }
            finally { image.UnregisterDirtyMaterialCallback(destroyCurrentEffect); }

            TraceMember("after removed effect's old install returns", ui, image, effect);
            TestContext.WriteLine("material restored on removal: " + ObjectName(materialAfterDestroy));
            Assert.IsTrue(destroyed, "The effect must be removed during its own material assignment.");
            Assert.IsFalse(effect);
            Assert.IsNull(failure, "Removing one effect must not fail the live panel: " + failure);
            Assert.AreSame(image.defaultMaterial, materialAfterDestroy,
                "Removal must restore the material installed by the removed effect.");
            Assert.AreSame(image.defaultMaterial, image.material);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [UnityTest]
        public IEnumerator CurrentMember_ReparentDuringMaterialReplacement_ShouldKeepDestinationOwnership()
        {
            var source = CreateGroup("ReplacingSource", out var sourceContent);
            var destination = CreateGroup("ReplacementDestination", out var destinationContent);
            var image = CreateMember(sourceContent, out var effect);
            yield return null;
            var originalMaterial = CreateMaterial("SourceOriginal");
            var replacementMaterial = CreateMaterial("SourceReplacement");
            var destinationMaterial = CreateMaterial("DestinationReplacement");
            SetTemplate(source, originalMaterial);
            SetTemplate(destination, destinationMaterial);
            image.material = null;
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            Assert.AreSame(originalMaterial, image.material);
            SetTemplate(source, replacementMaterial);

            var moved = false;
            Material materialAtCallback = null;
            Material materialAfterMove = null;
            UnityAction moveCurrentMember = () =>
            {
                if (moved) return;
                moved = true;
                materialAtCallback = image.material;
                image.transform.SetParent(destinationContent, false);
                materialAfterMove = image.material;
                TraceMember("replacement callback after reparent", destinationUI, image, effect);
            };
            image.RegisterDirtyMaterialCallback(moveCurrentMember);
            try
            {
                sourceUI.Hide();
                TraceMember("after interrupted source replacement", sourceUI, image, effect);
                Assert.IsTrue(moved);
                Assert.AreSame(image.defaultMaterial, materialAtCallback,
                    "The callback must run while the old installed template is being cleared.");
                Assert.AreSame(destinationMaterial, materialAfterMove);
                Assert.AreSame(destinationContent, image.transform.parent);
                Assert.AreSame(destinationMaterial, effect.materialPrefab,
                    "The interrupted template replacement must not overwrite the new owner configuration.");
                Assert.AreSame(destinationMaterial, image.material);
            }
            finally { image.UnregisterDirtyMaterialCallback(moveCurrentMember); }
        }

        [UnityTest]
        public IEnumerator Group_DestroyInsideMaterialRefresh_ShouldDetachAndFinish() =>
            VerifyGroupDestroyedInsideDirtyCallback(false);

        [UnityTest]
        public IEnumerator Group_DestroyInsideTerminalProgress_ShouldDetachAndFinish() =>
            VerifyGroupDestroyedInsideDirtyCallback(true);

        private IEnumerator VerifyGroupDestroyedInsideDirtyCallback(bool terminalProgressOnly)
        {
            var group = CreateGroup("DestroyGroup", out var content);
            var first = CreateMember(content, out var firstEffect);
            var later = CreateMember(content, out var laterEffect);
            later.rectTransform.anchoredPosition = new Vector2(200f, 0f);
            yield return null;
            group.EndScreenPoint = RectTransformUtility.WorldToScreenPoint(null, first.transform.position);
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            var destroyed = false;
            UnityAction destroyGroup = () =>
            {
                if (destroyed || (terminalProgressOnly && firstEffect.EffectAmount < 1f)) return;
                destroyed = true;
                Object.DestroyImmediate(group);
            };
            first.RegisterDirtyVerticesCallback(destroyGroup);
            Exception failure = null;
            try
            {
                try { ui.Hide(); }
                catch (Exception exception) { failure = exception; }
            }
            finally { first.UnregisterDirtyVerticesCallback(destroyGroup); }

            TraceMember("after group removal, first", ui, first, firstEffect);
            TraceMember("after group removal, later", ui, later, laterEffect);
            Assert.IsTrue(destroyed);
            Assert.IsFalse(group);
            Assert.IsNull(failure, "Removing the controller during a real dirty callback must finish safely: " + failure);
            Assert.AreEqual(UIState.Hided, ui.State);
            // Flow explicitly restores independent, visible meshes when their controller is removed.
            Assert.That(firstEffect.EffectAmount, Is.Zero);
            Assert.That(laterEffect.EffectAmount, Is.Zero);
            AssertVisible(first);
            AssertVisible(later);
        }

        [UnityTest]
        public IEnumerator CurrentMember_ReparentInsideOwnTerminalDirtyCallback_ShouldKeepDestinationState()
        {
            var source = CreateGroup("VertexSource", out var sourceContent);
            var destination = CreateGroup("VertexDestination", out var destinationContent);
            var image = CreateMember(sourceContent, out var effect);
            yield return null;
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            var moved = false;
            UnityAction moveCurrentMember = () =>
            {
                if (moved || effect.EffectAmount < 1f) return;
                moved = true;
                image.transform.SetParent(destinationContent, false);
            };
            image.RegisterDirtyVerticesCallback(moveCurrentMember);
            try
            {
                sourceUI.Hide();
                Assert.IsTrue(moved);
                Assert.AreSame(destinationContent, image.transform.parent);
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                Assert.That(effect.EffectAmount, Is.Zero);
                AssertVisible(image);
            }
            finally { image.UnregisterDirtyVerticesCallback(moveCurrentMember); }
        }

        [UnityTest]
        public IEnumerator Group_ReenableInsideMaterialRefresh_ShouldDeferHierarchyRefresh()
        {
            var group = CreateGroup("ReenabledGroup", out var content);
            var first = CreateMember(content, out var firstEffect);
            var later = CreateMember(content, out var laterEffect);
            yield return null;
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            var refreshed = false;
            Image added = null;
            UIFlowMeshEffect addedEffect = null;
            UnityAction refreshFromCallback = () =>
            {
                if (refreshed) return;
                refreshed = true;
                // Public component lifecycle invokes RefreshFlowHierarchy through OnEnable.
                // Adding while disabled ensures the next scan must actually discover a member.
                group.enabled = false;
                added = CreateMember(content, out addedEffect);
                group.enabled = true;
            };
            first.RegisterDirtyVerticesCallback(refreshFromCallback);
            try
            {
                Assert.DoesNotThrow(() => ui.Hide());
                Assert.IsTrue(refreshed);
                Assert.AreEqual(UIState.Hided, ui.State);
                Assert.That(firstEffect.EffectAmount, Is.EqualTo(1f));
                Assert.That(laterEffect.EffectAmount, Is.EqualTo(1f));
                Assert.That(addedEffect.EffectAmount, Is.EqualTo(1f));
                TraceMember("public reenable refresh, added", ui, added, addedEffect);
            }
            finally { first.UnregisterDirtyVerticesCallback(refreshFromCallback); }
        }

        private UIFlowGroupController CreateGroup(string name, out RectTransform content)
        {
            // Each panel has its own ordinary parent to exclude sibling mutual exclusion.
            var container = CreateRect(name + "Container", canvasRoot.transform);
            var group = CreateRect(name, container).gameObject.AddComponent<UIFlowGroupController>();
            content = CreateRect(name + "Content", group.transform);
            return group;
        }

        private Image CreateMember(Transform parent, out UIFlowMeshEffect effect)
        {
            var rect = CreateRect("Member", parent);
            rect.sizeDelta = new Vector2(100f, 60f);
            var image = rect.gameObject.AddComponent<Image>();
            // Suppress optional shader lookup and exercise CPU behavior until the test configures a template.
            image.material = CreateMaterial("MemberCustom");
            effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            return image;
        }

        private Material CreateMaterial(string name)
        {
            var material = new Material(Shader.Find("UI/Default")) { name = name + "_" + materials.Count };
            materials.Add(material);
            return material;
        }

        private static void TraceMember(string stage, UIBase panel, Image image, UIFlowMeshEffect effect)
        {
            // Read managed ownership fields even after the component's native object was removed.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var owner = typeof(UIFlowMeshEffect).GetField("groupController", flags).GetValue(effect) as Object;
            var installed = typeof(UIFlowMeshEffect).GetField("installedSharedFlowMaterial", flags).GetValue(effect) as Object;
            TestContext.WriteLine(stage + ": panel=" + ObjectName(panel) + ", state=" + panel.State +
                ", effectAlive=" + (bool)effect + ", parent=" + ObjectName(image.transform.parent) +
                ", trackedOwner=" + ObjectName(owner) + ", amount=" + effect.EffectAmount +
                ", template=" + ObjectName(effect.materialPrefab) + ", installed=" + ObjectName(installed) +
                ", actual=" + ObjectName(image.material) + ", default=" + ObjectName(image.defaultMaterial));
        }

        private static string ObjectName(Object value)
        {
            if (ReferenceEquals(value, null)) return "<null>";
            return value ? value.name : "<destroyed>";
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void SetTemplate(UIFlowGroupController group, Material material)
        {
            var field = typeof(UIFlowGroupController).GetField(
                "materialPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(group, material);
        }

        private static void AssertVisible(Image image)
        {
            image.Rebuild(CanvasUpdate.PreRender);
            var mesh = image.canvasRenderer.GetMesh();
            Assert.NotNull(mesh);
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            foreach (var color in mesh.colors32) Assert.That(color.a, Is.EqualTo(255));
        }
    }
}
