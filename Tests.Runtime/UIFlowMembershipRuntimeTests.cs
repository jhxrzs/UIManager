using System.Collections;
using System.Collections.Generic;
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
