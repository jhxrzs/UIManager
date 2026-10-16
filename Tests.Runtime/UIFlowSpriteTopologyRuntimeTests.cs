using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public class UIFlowSpriteTopologyRuntimeTests
    {
        private GameObject canvasRoot;
        private readonly List<Object> assets = new();

        [TearDown]
        public void TearDown()
        {
            if (canvasRoot) Object.DestroyImmediate(canvasRoot);
            for (var i = assets.Count - 1; i >= 0; --i)
                if (assets[i]) Object.DestroyImmediate(assets[i]);
            assets.Clear();
        }

        [TestCase(0.000001f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void TightFourVertexSprite_ShouldPreserveItsOriginalTopology(float progress)
        {
            var image = CreateImage();
            image.sprite = CreateDiamondSprite();
            image.useSpriteMesh = true;
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            image.Rebuild(CanvasUpdate.PreRender);
            var original = image.canvasRenderer.GetMesh();
            var originalBounds = original.bounds;
            var originalTriangles = original.triangles;
            Assert.That(original.vertexCount, Is.EqualTo(4));
            Assert.That(originalBounds.size.x, Is.EqualTo(100f).Within(0.01f));
            Assert.That(originalBounds.size.y, Is.EqualTo(100f).Within(0.01f));

            effect.EndScreenPoint = new Vector3(600, 300, 0);
            effect.EffectAmount = progress;
            image.Rebuild(CanvasUpdate.PreRender);
            var actual = image.canvasRenderer.GetMesh();
            Assert.That(actual.vertexCount, Is.EqualTo(4));
            CollectionAssert.AreEqual(originalTriangles, actual.triangles);
            if (progress < 0.00001f)
            {
                Assert.That(Vector3.Distance(originalBounds.min, actual.bounds.min), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(originalBounds.max, actual.bounds.max), Is.LessThan(0.01f));
            }
            if (progress == 1f)
                foreach (var color in actual.colors32) Assert.That(color.a, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RectangularSimpleImage_ShouldStillUseSubdividedMesh(bool hasSprite)
        {
            var image = CreateImage();
            if (hasSprite) image.sprite = CreateDiamondSprite();
            // Without Sprite Mesh, Image renders a rectangle even if its Sprite has custom geometry.
            image.useSpriteMesh = false;
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            effect.EndScreenPoint = new Vector3(600, 300, 0);
            effect.EffectAmount = 0.5f;
            image.Rebuild(CanvasUpdate.PreRender);
            Assert.That(image.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(4));
        }

        private Image CreateImage()
        {
            canvasRoot = new GameObject("SpriteFlowCanvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("SpriteFlowImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasRoot.transform, false);
            var image = go.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(100, 100);
            var material = new Material(Shader.Find("UI/Default"));
            assets.Add(material);
            image.material = material;
            return image;
        }

        private Sprite CreateDiamondSprite()
        {
            var texture = new Texture2D(2, 2);
            assets.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            assets.Add(sprite);
            // OverrideGeometry uses pixel positions inside the Sprite Rect, before pivot/PPU conversion.
            sprite.OverrideGeometry(new[]
            {
                new Vector2(0, 1), new Vector2(1, 2), new Vector2(2, 1), new Vector2(1, 0)
            }, new ushort[] { 0, 1, 2, 0, 2, 3 });
            return sprite;
        }
    }
}
