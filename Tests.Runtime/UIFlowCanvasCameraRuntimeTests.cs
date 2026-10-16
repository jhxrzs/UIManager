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
    public class UIFlowCanvasCameraRuntimeTests
    {
        private readonly List<GameObject> roots = new();
        private readonly List<Material> materials = new();
        private readonly List<GameObject> previousMainCameras = new();
        private static readonly PropertyInfo ProgressProperty = typeof(EffectControllerBase)
            .GetProperty("EffectAmount", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = roots.Count - 1; i >= 0; --i) if (roots[i]) Object.Destroy(roots[i]);
            roots.Clear();
            yield return null;
            foreach (var material in materials) if (material) Object.Destroy(material);
            materials.Clear();
            foreach (var camera in previousMainCameras) if (camera) camera.tag = "MainCamera";
            previousMainCameras.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldSpace_MainCameraFallback_ShouldMatchSameExplicitCamera()
        {
            var camera = CreateCamera(true);
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            var group = CreateGroup(canvas.transform, out var image);
            yield return null;
            group.GetComponent<UIBase>().Show();
            group.EndScreenPoint = camera.WorldToScreenPoint(new Vector3(3, 1, 0));
            var implicitCameraVertices = Sample(group, image, 0.5f);

            canvas.worldCamera = camera;
            var explicitCameraVertices = Sample(group, image, 0.5f);
            AssertVerticesEqual(implicitCameraVertices, explicitCameraVertices);
        }

        [UnityTest]
        public IEnumerator WorldSpace_RuntimeCameraChange_ShouldMatchFreshGroup()
        {
            var main = CreateCamera(true);
            var other = CreateCamera(false);
            other.transform.position = new Vector3(2, -1, -10);
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            canvas.worldCamera = main;
            var group = CreateGroup(canvas.transform, out var image);
            yield return null;
            group.GetComponent<UIBase>().Show();
            group.EndScreenPoint = new Vector3(470, 300, 0);
            Sample(group, image, 0.5f);

            canvas.worldCamera = other;
            var fresh = CreateGroup(canvas.transform, out var freshImage);
            fresh.GetComponent<UIBase>().Show();
            fresh.EndScreenPoint = group.EndScreenPoint;
            var expected = Sample(fresh, freshImage, 0.5f);
            var actual = Sample(group, image, 0.5f);
            AssertVerticesEqual(expected, actual);
        }

        [UnityTest]
        public IEnumerator NestedCanvas_ShouldUseRootCanvasCameraForGroupAndMesh()
        {
            var camera = CreateCamera(true);
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            canvas.worldCamera = camera;
            var direct = CreateGroup(canvas.transform, out var directImage);
            var nestedRoot = CreateRect("NestedCanvas", canvas.transform);
            nestedRoot.gameObject.AddComponent<Canvas>().overrideSorting = true;
            var nested = CreateGroup(nestedRoot, out var nestedImage);
            yield return null;
            direct.GetComponent<UIBase>().Show();
            nested.GetComponent<UIBase>().Show();
            direct.EndScreenPoint = camera.WorldToScreenPoint(new Vector3(3, 1, 0));
            nested.EndScreenPoint = direct.EndScreenPoint;
            AssertVerticesEqual(Sample(direct, directImage, 0.5f), Sample(nested, nestedImage, 0.5f));
        }

        [UnityTest]
        public IEnumerator Overlay_ShouldIgnoreAssignedAndMainCameras()
        {
            var camera = CreateCamera(true);
            var canvas = CreateCanvas(RenderMode.ScreenSpaceOverlay);
            canvas.worldCamera = camera;
            var group = CreateGroup(canvas.transform, out var image);
            yield return null;
            group.GetComponent<UIBase>().Show();
            group.EndScreenPoint = new Vector3(470, 300, 0);
            AssertScreenEndpoint(group, image, null);
        }

        [UnityTest]
        public IEnumerator ScreenSpaceCameraWithoutAssignedCamera_ShouldUseOverlayCoordinates()
        {
            CreateCamera(true);
            var canvas = CreateCanvas(RenderMode.ScreenSpaceCamera);
            canvas.worldCamera = null;
            var group = CreateGroup(canvas.transform, out var image);
            yield return null;
            group.GetComponent<UIBase>().Show();
            group.EndScreenPoint = new Vector3(470, 300, 0);
            AssertScreenEndpoint(group, image, null);
        }

        private Camera CreateCamera(bool makeMain)
        {
            if (makeMain)
                while (Camera.main)
                {
                    var previous = Camera.main.gameObject;
                    previousMainCameras.Add(previous);
                    previous.tag = "Untagged";
                }
            var go = new GameObject("FlowCamera", typeof(Camera));
            roots.Add(go);
            if (makeMain) go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.pixelRect = new Rect(0, 0, 640, 480);
            if (makeMain) Assert.That(Camera.main, Is.SameAs(camera));
            return camera;
        }

        private Canvas CreateCanvas(RenderMode mode)
        {
            var go = new GameObject("FlowCanvas", typeof(RectTransform), typeof(Canvas));
            roots.Add(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = mode;
            if (mode == RenderMode.WorldSpace) ((RectTransform)go.transform).sizeDelta = new Vector2(10, 10);
            return canvas;
        }

        private UIFlowGroupController CreateGroup(Transform parent, out Image image)
        {
            // Separate plain parents avoid unrelated UIBase mutual exclusion.
            var container = CreateRect("GroupContainer", parent);
            var group = CreateRect("FlowGroup", container).gameObject.AddComponent<UIFlowGroupController>();
            var rect = CreateRect("FlowImage", group.transform);
            rect.sizeDelta = Vector2.one;
            image = rect.gameObject.AddComponent<Image>();
            var material = new Material(Shader.Find("UI/Default"));
            materials.Add(material);
            image.material = material;
            image.gameObject.AddComponent<UIFlowMeshEffect>();
            return group;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Vector3[] Sample(UIFlowGroupController group, Image image, float progress)
        {
            Assert.NotNull(ProgressProperty);
            // Only the timeline position is sampled; geometry is rebuilt through the real Graphic pipeline.
            ProgressProperty.SetValue(group, progress);
            image.Rebuild(CanvasUpdate.PreRender);
            var mesh = image.canvasRenderer.GetMesh();
            Assert.NotNull(mesh);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            return mesh.vertices;
        }

        private static void AssertScreenEndpoint(UIFlowGroupController group, Image image, Camera camera)
        {
            Assert.IsTrue(RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,
                group.EndScreenPoint, camera, out var expected));
            var vertices = Sample(group, image, 1f);
            foreach (var vertex in vertices)
                Assert.That(Vector2.Distance(expected, vertex), Is.LessThan(0.001f));
        }

        private static void AssertVerticesEqual(Vector3[] expected, Vector3[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; ++i)
                Assert.That(Vector3.Distance(expected[i], actual[i]), Is.LessThan(0.001f));
        }
    }
}
