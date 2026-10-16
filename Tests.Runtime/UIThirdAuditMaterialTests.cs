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
    public class UIThirdAuditMaterialTests
    {
        private GameObject canvasRoot;
        private readonly List<Material> materials = new();

        [SetUp]
        public void SetUp()
        {
            canvasRoot = new GameObject("ThirdAuditMaterialCanvas", typeof(RectTransform), typeof(Canvas));
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
        public IEnumerator Install_ThreeGroupNestedMigration_ShouldKeepLastMaterialAndCleanupOwnership()
        {
            var first = CreateGroup("First", out var firstContent);
            var middle = CreateGroup("Middle", out var middleContent);
            var last = CreateGroup("Last", out var lastContent);
            var image = CreateMember(firstContent, out var effect);
            yield return null;
            var firstUI = first.GetComponent<UIBase>();
            var middleUI = middle.GetComponent<UIBase>();
            var lastUI = last.GetComponent<UIBase>();
            firstUI.Show();
            middleUI.Show();
            lastUI.Show();
            var firstMaterial = CreateMaterial("FirstInstall");
            var middleMaterial = CreateMaterial("MiddleInstall");
            var lastMaterial = CreateMaterial("LastInstall");
            SetTemplate(first, firstMaterial);
            SetTemplate(middle, middleMaterial);
            SetTemplate(last, lastMaterial);
            image.material = null;

            var moves = 0;
            UnityAction migrate = () =>
            {
                // Ignore the nested callbacks that clear the old shared material.
                if (moves == 0 && image.material == firstMaterial)
                {
                    moves = 1;
                    image.transform.SetParent(middleContent, false);
                }
                else if (moves == 1 && image.material == middleMaterial)
                {
                    moves = 2;
                    image.transform.SetParent(lastContent, false);
                }
            };
            image.RegisterDirtyMaterialCallback(migrate);
            Exception failure;
            try { failure = Capture(() => firstUI.Hide()); }
            finally { image.UnregisterDirtyMaterialCallback(migrate); }

            Trace("three nested groups returned", firstUI, image, effect);
            Assert.IsNull(failure, failure?.ToString());
            Assert.That(moves, Is.EqualTo(2), "Both migrations must happen inside real material setter callbacks.");
            Assert.AreSame(lastContent, image.transform.parent);
            Assert.AreEqual(UIState.Hided, firstUI.State);
            Assert.AreEqual(UIState.Showed, lastUI.State);
            Assert.That(effect.EffectAmount, Is.Zero);
            Assert.AreSame(lastMaterial, effect.materialPrefab);
            Assert.AreSame(lastMaterial, image.material);
            AssertVisible(image);
            Object.DestroyImmediate(effect);
            Assert.AreSame(image.defaultMaterial, image.material,
                "The final shared material must retain cleanup ownership after both older calls return.");
        }

        [UnityTest]
        public IEnumerator Install_UserCustomMaterialThenMigrate_ShouldPreserveOverrideThroughReplacementAndRemoval()
        {
            var source = CreateGroup("CustomSource", out var sourceContent);
            var destination = CreateGroup("CustomDestination", out var destinationContent);
            var image = CreateMember(sourceContent, out var effect);
            yield return null;
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            var sourceMaterial = CreateMaterial("CustomSourceInstall");
            var destinationMaterial = CreateMaterial("CustomDestinationInstall");
            var userMaterial = CreateMaterial("UserOverrideInsideSetter");
            SetTemplate(source, sourceMaterial);
            SetTemplate(destination, destinationMaterial);
            image.material = null;

            var changed = false;
            UnityAction overrideAndMigrate = () =>
            {
                if (changed || image.material != sourceMaterial) return;
                changed = true;
                image.material = userMaterial;
                image.transform.SetParent(destinationContent, false);
            };
            image.RegisterDirtyMaterialCallback(overrideAndMigrate);
            Exception failure;
            try { failure = Capture(() => sourceUI.Hide()); }
            finally { image.UnregisterDirtyMaterialCallback(overrideAndMigrate); }

            Trace("custom override after migration", destinationUI, image, effect);
            Assert.IsNull(failure, failure?.ToString());
            Assert.IsTrue(changed);
            Assert.AreSame(destinationContent, image.transform.parent);
            Assert.AreSame(destinationMaterial, effect.materialPrefab);
            Assert.AreSame(userMaterial, image.material);
            Assert.That(effect.EffectAmount, Is.Zero);
            var nextTemplate = CreateMaterial("NextDestinationTemplate");
            SetTemplate(destination, nextTemplate);
            destinationUI.Hide();
            Assert.That(effect.EffectAmount, Is.EqualTo(1f));
            Assert.AreSame(nextTemplate, effect.materialPrefab);
            Assert.AreSame(userMaterial, image.material);
            destinationUI.Show();
            Object.DestroyImmediate(effect);
            Assert.AreSame(userMaterial, image.material,
                "Later template replacement and effect removal must preserve a synchronous user override.");
        }

        [UnityTest]
        public IEnumerator Install_DestroyWholeGraphicObject_ShouldFinishPanelAndUpdateSurvivingMember()
        {
            var group = CreateGroup("DestroyGraphic", out var content);
            var image = CreateMember(content, out var effect);
            var survivingImage = CreateMember(content, out var survivingEffect);
            survivingImage.rectTransform.anchoredPosition = new Vector2(220f, 0f);
            yield return null;
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            var shared = CreateMaterial("DestroyGraphicInstall");
            SetTemplate(group, shared);
            image.material = null;
            survivingImage.material = null;

            var destroyed = false;
            UnityAction destroyGraphic = () =>
            {
                if (destroyed || image.material != shared) return;
                destroyed = true;
                Object.DestroyImmediate(image.gameObject);
            };
            image.RegisterDirtyMaterialCallback(destroyGraphic);
            Exception failure;
            try { failure = Capture(() => ui.Hide()); }
            finally { if (image) image.UnregisterDirtyMaterialCallback(destroyGraphic); }

            Trace("after whole Graphic object removal", ui, survivingImage, survivingEffect);
            Assert.IsTrue(destroyed);
            Assert.IsFalse(image);
            Assert.IsFalse(effect);
            Assert.IsNull(failure, "The surviving panel must finish after its current Graphic is removed: " + failure);
            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.That(survivingEffect.EffectAmount, Is.EqualTo(1f));
            Assert.AreSame(shared, survivingImage.material);
            ui.Show();
            Assert.That(survivingEffect.EffectAmount, Is.Zero);
            AssertVisible(survivingImage);
        }

        [UnityTest]
        public IEnumerator Replacement_DestroyEffectWhileOldMaterialClears_ShouldNotInstallReplacement()
        {
            var group = CreateGroup("DestroyDuringClear", out var content);
            var image = CreateMember(content, out var effect);
            yield return null;
            var oldMaterial = CreateMaterial("OldBeforeClear");
            var replacementMaterial = CreateMaterial("ReplacementAfterClear");
            SetTemplate(group, oldMaterial);
            image.material = null;
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            Assert.AreSame(oldMaterial, image.material);
            SetTemplate(group, replacementMaterial);

            var destroyed = false;
            Material materialAtCallback = null;
            UnityAction destroyWhileClearing = () =>
            {
                if (destroyed) return;
                destroyed = true;
                materialAtCallback = image.material;
                Object.DestroyImmediate(effect);
            };
            image.RegisterDirtyMaterialCallback(destroyWhileClearing);
            Exception failure;
            try { failure = Capture(() => ui.Hide()); }
            finally { image.UnregisterDirtyMaterialCallback(destroyWhileClearing); }

            Trace("after old material clear destroyed effect", ui, image, effect);
            Assert.IsTrue(destroyed);
            Assert.AreSame(image.defaultMaterial, materialAtCallback,
                "The effect must be removed in the old template's clearing callback, before replacement installation.");
            Assert.IsFalse(effect);
            Assert.IsNull(failure, failure?.ToString());
            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.AreSame(image.defaultMaterial, image.material,
                "The suspended replacement must not install a material on behalf of a destroyed component.");
            AssertVisible(image);
        }

        [UnityTest]
        public IEnumerator Install_DestroyThenAddEffectOnSameNode_ShouldDriveReplacementAndPreserveItsCustomMaterial()
        {
            var group = CreateGroup("ReplaceComponent", out var content);
            var image = CreateMember(content, out var originalEffect);
            yield return null;
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            var shared = CreateMaterial("RemovedComponentShared");
            var replacementCustom = CreateMaterial("ReplacementComponentCustom");
            SetTemplate(group, shared);
            image.material = null;

            var replaced = false;
            UIFlowMeshEffect replacementEffect = null;
            Material afterRemoval = null;
            UnityAction replaceEffect = () =>
            {
                if (replaced || image.material != shared) return;
                replaced = true;
                Object.DestroyImmediate(originalEffect);
                afterRemoval = image.material;
                // The new component starts with an explicit user material, as a pooled icon can.
                image.material = replacementCustom;
                replacementEffect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            };
            image.RegisterDirtyMaterialCallback(replaceEffect);
            Exception failure;
            try { failure = Capture(() => ui.Hide()); }
            finally { image.UnregisterDirtyMaterialCallback(replaceEffect); }

            Trace("after same-node effect replacement", ui, image, replacementEffect);
            Assert.IsTrue(replaced);
            Assert.IsNull(failure, failure?.ToString());
            Assert.IsFalse(originalEffect);
            Assert.IsTrue(replacementEffect);
            Assert.AreSame(image.defaultMaterial, afterRemoval);
            Assert.AreEqual(1, image.GetComponents<UIFlowMeshEffect>().Length);
            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.That(replacementEffect.EffectAmount, Is.EqualTo(1f),
                "A newly registered component must receive the pending terminal state, despite the dead cache entry.");
            Assert.AreSame(shared, replacementEffect.materialPrefab);
            Assert.AreSame(replacementCustom, image.material);
            ui.Show();
            Assert.That(replacementEffect.EffectAmount, Is.Zero);
            AssertVisible(image);
            Object.DestroyImmediate(replacementEffect);
            Assert.AreSame(replacementCustom, image.material);
        }

        [UnityTest]
        public IEnumerator Install_MoveOutsideAllGroups_ShouldRemainVisibleAndReleaseInstalledMaterialOnRemoval()
        {
            var source = CreateGroup("LeaveAllGroups", out var content);
            var ungrouped = CreateRect("IndependentContent", canvasRoot.transform);
            var image = CreateMember(content, out var effect);
            yield return null;
            var ui = source.GetComponent<UIBase>();
            ui.Show();
            var shared = CreateMaterial("LeavingGroupInstall");
            SetTemplate(source, shared);
            image.material = null;

            var moved = false;
            UnityAction leaveGroup = () =>
            {
                if (moved || image.material != shared) return;
                moved = true;
                image.transform.SetParent(ungrouped, false);
            };
            image.RegisterDirtyMaterialCallback(leaveGroup);
            Exception failure;
            try { failure = Capture(() => ui.Hide()); }
            finally { image.UnregisterDirtyMaterialCallback(leaveGroup); }

            Trace("after moving outside all groups", ui, image, effect);
            Assert.IsNull(failure, failure?.ToString());
            Assert.IsTrue(moved);
            Assert.AreSame(ungrouped, image.transform.parent);
            Assert.IsNull(image.GetComponentInParent<UIFlowGroupController>(true));
            // DetachGroupController explicitly restores independent visible geometry.
            // Detachment has no contract to replace/reset the material, so do not require that here.
            Assert.That(effect.EffectAmount, Is.Zero);
            AssertVisible(image);
            ui.Show();
            ui.Hide();
            Assert.That(effect.EffectAmount, Is.Zero, "The former group must no longer drive the detached member.");
            Object.DestroyImmediate(effect);
            Assert.AreSame(image.defaultMaterial, image.material,
                "Leaving a group during installation must not lose the effect's material cleanup record.");
        }

        [UnityTest]
        public IEnumerator Install_MoveToDisabledGroup_ShouldStayDetachedUntilDestinationReenabled()
        {
            var source = CreateGroup("DisabledDestinationSource", out var sourceContent);
            var destination = CreateGroup("DisabledDestination", out var destinationContent);
            var image = CreateMember(sourceContent, out var effect);
            yield return null;
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            destinationUI.Hide();
            destination.enabled = false;
            var sourceMaterial = CreateMaterial("BeforeDisabledDestination");
            var destinationMaterial = CreateMaterial("AfterDestinationReenabled");
            SetTemplate(source, sourceMaterial);
            SetTemplate(destination, destinationMaterial);
            image.material = null;

            var moved = false;
            UnityAction moveToDisabled = () =>
            {
                if (moved || image.material != sourceMaterial) return;
                moved = true;
                image.transform.SetParent(destinationContent, false);
            };
            image.RegisterDirtyMaterialCallback(moveToDisabled);
            Exception failure;
            try { failure = Capture(() => sourceUI.Hide()); }
            finally { image.UnregisterDirtyMaterialCallback(moveToDisabled); }

            Trace("inside disabled destination", destinationUI, image, effect);
            Assert.IsNull(failure, failure?.ToString());
            Assert.IsTrue(moved);
            Assert.AreSame(destinationContent, image.transform.parent);
            Assert.IsFalse(destination.isActiveAndEnabled);
            Assert.AreEqual(UIState.Hided, destinationUI.State);
            // Registration intentionally ignores disabled controllers; OnEnable performs the takeover.
            Assert.That(effect.EffectAmount, Is.Zero);
            AssertVisible(image);
            destination.enabled = true;
            Trace("destination reenabled", destinationUI, image, effect);
            Assert.That(effect.EffectAmount, Is.EqualTo(1f));
            Assert.AreSame(destinationMaterial, effect.materialPrefab);
            Assert.AreSame(destinationMaterial, image.material);
            destinationUI.Show();
            Assert.That(effect.EffectAmount, Is.Zero);
            Object.DestroyImmediate(effect);
            Assert.AreSame(image.defaultMaterial, image.material);
        }

        private UIFlowGroupController CreateGroup(string name, out RectTransform content)
        {
            // Separate ordinary parents prevent sibling UI mutual exclusion from affecting the probe.
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
            image.material = CreateMaterial("SetupCustom");
            effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            return image;
        }

        private Material CreateMaterial(string name)
        {
            var shader = Shader.Find("UI/Default");
            Assert.NotNull(shader);
            var material = new Material(shader) { name = name + "_" + materials.Count };
            materials.Add(material);
            return material;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void SetTemplate(UIFlowGroupController group, Material material)
        {
            var field = typeof(UIFlowGroupController).GetField("materialPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(group, material);
        }

        private static Exception Capture(Action operation)
        {
            try { operation(); return null; }
            catch (Exception failure) { return failure; }
        }

        private static void Trace(string stage, UIBase panel, Image image, UIFlowMeshEffect effect)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var owner = ReferenceEquals(effect, null) ? null :
                typeof(UIFlowMeshEffect).GetField("groupController", flags).GetValue(effect) as Object;
            var installed = ReferenceEquals(effect, null) ? null :
                typeof(UIFlowMeshEffect).GetField("installedSharedFlowMaterial", flags).GetValue(effect) as Object;
            TestContext.WriteLine(stage + ": state=" + panel.State + ", effectAlive=" + (bool)effect +
                ", parent=" + Name(image.transform.parent) + ", owner=" + Name(owner) +
                ", amount=" + (ReferenceEquals(effect, null) ? "<null>" : effect.EffectAmount.ToString()) +
                ", template=" + (ReferenceEquals(effect, null) ? "<null>" : Name(effect.materialPrefab)) +
                ", installed=" + Name(installed) + ", actual=" + Name(image.material));
        }

        private static string Name(Object value) => ReferenceEquals(value, null) ? "<null>" : value ? value.name : "<destroyed>";

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
