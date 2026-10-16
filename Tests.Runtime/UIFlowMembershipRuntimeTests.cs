using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public class UIFlowMembershipRuntimeTests
    {
        private GameObject canvasRoot;
        private readonly List<Material> materials = new();

        [SetUp]
        public void SetUp()
        {
            canvasRoot = new GameObject("MembershipCanvas", typeof(RectTransform), typeof(Canvas));
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
        public IEnumerator HiddenGroup_NewDeepMember_ShouldBeHiddenBeforeFirstRebuild()
        {
            var group = CreateGroup("HiddenGroup");
            var content = CreateRect("Content", group.transform);
            yield return null;
            Hide(group);
            var image = CreateImage(content);
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();

            AssertHidden(image, effect);
            yield return null;
            group.GetComponent<UIBase>().Hide();
            AssertHidden(image, effect);
        }

        [UnityTest]
        public IEnumerator InactiveMemberAndDisabledEffect_ShouldSynchronizeWhenEnabled()
        {
            var group = CreateGroup("HiddenGroup");
            var content = CreateRect("Content", group.transform);
            yield return null;
            Hide(group);
            var image = CreateImage(content);
            image.gameObject.SetActive(false);
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            image.gameObject.SetActive(true);
            AssertHidden(image, effect);

            effect.enabled = false;
            AssertVisible(image);
            effect.enabled = true;
            AssertHidden(image, effect);

            group.enabled = false;
            var laterImage = CreateImage(content);
            var laterEffect = laterImage.gameObject.AddComponent<UIFlowMeshEffect>();
            AssertVisible(laterImage);
            group.enabled = true;
            AssertHidden(laterImage, laterEffect);
            AssertHidden(image, effect);
        }

        [UnityTest]
        public IEnumerator ReparentingIntoHiddenGroup_ShouldUseNewOwnerImmediately()
        {
            var source = CreateGroup("Source");
            var sourceContent = CreateRect("SourceContent", source.transform);
            var destination = CreateGroup("Destination");
            var destinationContent = CreateRect("DestinationContent", destination.transform);
            var image = CreateImage(sourceContent);
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            yield return null;
            source.GetComponent<UIBase>().Show();
            Hide(destination);
            AssertVisible(image);

            image.transform.SetParent(destinationContent, false);
            AssertHidden(image, effect);
            source.GetComponent<UIBase>().Hide();
            source.GetComponent<UIBase>().Show();
            AssertHidden(image, effect);
        }

        [UnityTest]
        public IEnumerator NewEffectOnControllerObject_ShouldRegisterItself()
        {
            var group = CreateGroup("SameObject");
            var image = group.gameObject.AddComponent<Image>();
            UseCustomMaterial(image);
            yield return null;
            Hide(group);
            var effect = group.gameObject.AddComponent<UIFlowMeshEffect>();
            AssertHidden(image, effect);
        }

        [UnityTest]
        public IEnumerator MemberCreatedInsideGraphicDirtyCallback_ShouldNotInvalidateGroupTraversal()
        {
            var group = CreateGroup("ReentrantGroup");
            var content = CreateRect("Content", group.transform);
            var first = CreateImage(content);
            first.gameObject.AddComponent<UIFlowMeshEffect>();
            yield return null;
            group.GetComponent<UIBase>().Show();
            Image second = null;
            Image third = null;
            var added = false;
            UnityEngine.Events.UnityAction addMembers = () =>
            {
                if (added) return;
                added = true;
                second = CreateImage(content);
                second.gameObject.AddComponent<UIFlowMeshEffect>();
                third = CreateImage(content);
                third.gameObject.AddComponent<UIFlowMeshEffect>();
            };
            first.RegisterDirtyVerticesCallback(addMembers);
            try
            {
                group.GetComponent<UIBase>().Hide();
                Assert.IsTrue(added, "The real Graphic dirty callback must have created members during traversal.");
                AssertHidden(first, first.GetComponent<UIFlowMeshEffect>());
                AssertHidden(second, second.GetComponent<UIFlowMeshEffect>());
                AssertHidden(third, third.GetComponent<UIFlowMeshEffect>());
            }
            finally
            {
                first.UnregisterDirtyVerticesCallback(addMembers);
            }
        }

        [UnityTest]
        public IEnumerator MemberReparentedInsideGraphicDirtyCallback_ShouldKeepNewGroupState()
        {
            var source = CreateGroup("ReentrantSource");
            var sourceContent = CreateRect("SourceContent", source.transform);
            var destination = CreateGroup("ReentrantDestination");
            var destinationContent = CreateRect("DestinationContent", destination.transform);
            var first = CreateImage(sourceContent);
            var firstEffect = first.gameObject.AddComponent<UIFlowMeshEffect>();
            var later = CreateImage(sourceContent);
            var laterEffect = later.gameObject.AddComponent<UIFlowMeshEffect>();
            later.rectTransform.anchoredPosition = new Vector2(200f, 0f);
            yield return null;

            // Distance sorting must visit first before later, independently of Unity entity IDs.
            source.EndScreenPoint = RectTransformUtility.WorldToScreenPoint(null, first.transform.position);
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            AssertVisible(first);
            AssertVisible(later);

            var moved = false;
            var progressBeforeMove = -1f;
            var progressAfterMove = -1f;
            UnityEngine.Events.UnityAction moveLaterMember = () =>
            {
                // Hide first refreshes materials at progress zero. Move only during its final
                // progress traversal, after that traversal has captured both sorted members.
                if (moved || firstEffect.EffectAmount < 1f) return;
                moved = true;
                progressBeforeMove = laterEffect.EffectAmount;
                // Moving beneath Content changes neither controller's direct children.
                later.transform.SetParent(destinationContent, false);
                progressAfterMove = laterEffect.EffectAmount;
            };
            first.RegisterDirtyVerticesCallback(moveLaterMember);
            try
            {
                sourceUI.Hide();
                Assert.IsTrue(moved, "The actual Graphic dirty callback must move the later member.");
                Assert.That(progressBeforeMove, Is.Zero, "The moved member must still await its old-group turn.");
                Assert.That(progressAfterMove, Is.Zero, "Reparenting must first apply the shown destination state.");
                Assert.AreSame(destinationContent, later.transform.parent);
                Assert.AreEqual(UIState.Hided, sourceUI.State);
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                Assert.That(laterEffect.EffectAmount, Is.Zero,
                    "The old group's remaining traversal must not overwrite the new group's shown state.");
                AssertVisible(later);
                AssertHidden(first, firstEffect);
            }
            finally
            {
                first.UnregisterDirtyVerticesCallback(moveLaterMember);
            }
        }

        [UnityTest]
        public IEnumerator MemberReparentedInsideMaterialRefresh_ShouldKeepNewGroupMaterial()
        {
            var source = CreateGroup("MaterialSource");
            var sourceContent = CreateRect("SourceContent", source.transform);
            var destination = CreateGroup("MaterialDestination");
            var destinationContent = CreateRect("DestinationContent", destination.transform);
            var first = CreateImage(sourceContent);
            first.gameObject.AddComponent<UIFlowMeshEffect>();
            var later = CreateImage(sourceContent);
            var laterEffect = later.gameObject.AddComponent<UIFlowMeshEffect>();
            yield return null;

            var sourceMaterial = new Material(Shader.Find("UI/Default"));
            var destinationMaterial = new Material(Shader.Find("UI/Default"));
            materials.Add(sourceMaterial);
            materials.Add(destinationMaterial);
            var templateField = typeof(UIFlowGroupController).GetField(
                "materialPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(templateField);
            templateField.SetValue(source, sourceMaterial);
            templateField.SetValue(destination, destinationMaterial);
            // Use default Graphic materials so the real shared-material installation path runs.
            first.material = null;
            later.material = null;
            var sourceUI = source.GetComponent<UIBase>();
            var destinationUI = destination.GetComponent<UIBase>();
            sourceUI.Show();
            destinationUI.Show();
            Assert.AreSame(sourceMaterial, first.material);
            Assert.AreSame(sourceMaterial, later.material);

            var moved = false;
            Material templateAfterMove = null;
            Material graphicMaterialAfterMove = null;
            UnityEngine.Events.UnityAction moveLaterMember = () =>
            {
                if (moved) return;
                moved = true;
                // Material refresh follows hierarchy order, so first's dirty callback runs
                // before the old controller reaches later in the same refresh traversal.
                later.transform.SetParent(destinationContent, false);
                templateAfterMove = laterEffect.materialPrefab;
                graphicMaterialAfterMove = later.material;
            };
            first.RegisterDirtyVerticesCallback(moveLaterMember);
            try
            {
                sourceUI.Hide();
                Assert.IsTrue(moved, "The material refresh must invoke the real Graphic dirty callback.");
                Assert.AreSame(destinationContent, later.transform.parent);
                Assert.AreEqual(UIState.Showed, destinationUI.State);
                Assert.AreSame(destinationMaterial, templateAfterMove);
                Assert.AreSame(destinationMaterial, graphicMaterialAfterMove);
                Assert.AreSame(destinationMaterial, laterEffect.materialPrefab,
                    "The old material traversal must not replace the new group's template.");
                Assert.AreSame(destinationMaterial, later.material,
                    "The old material traversal must not reinstall its material on a moved Graphic.");
            }
            finally
            {
                first.UnregisterDirtyVerticesCallback(moveLaterMember);
            }
        }

        private UIFlowGroupController CreateGroup(string name)
        {
            // Separate plain parents keep these panels outside each other's mutual-exclusion scope.
            var parent = CreateRect(name + "Container", canvasRoot.transform);
            return CreateRect(name, parent).gameObject.AddComponent<UIFlowGroupController>();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Image CreateImage(Transform parent)
        {
            var rect = CreateRect("Member", parent);
            rect.sizeDelta = new Vector2(100, 60);
            var image = rect.gameObject.AddComponent<Image>();
            UseCustomMaterial(image);
            return image;
        }

        private void UseCustomMaterial(Image image)
        {
            var material = new Material(Shader.Find("UI/Default"));
            materials.Add(material);
            image.material = material;
        }

        private static void Hide(UIFlowGroupController group)
        {
            var ui = group.GetComponent<UIBase>();
            ui.Show();
            ui.Hide();
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        private static void AssertHidden(Image image, UIFlowMeshEffect effect)
        {
            Assert.That(effect.EffectAmount, Is.EqualTo(1f));
            image.Rebuild(CanvasUpdate.PreRender);
            var mesh = image.canvasRenderer.GetMesh();
            Assert.NotNull(mesh);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            foreach (var color in mesh.colors32) Assert.That(color.a, Is.Zero);
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
