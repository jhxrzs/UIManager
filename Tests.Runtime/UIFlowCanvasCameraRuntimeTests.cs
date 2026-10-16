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
    /// <summary>验证不同 Canvas 模式及相机变化下的流动坐标一致性。</summary>
    public class UIFlowCanvasCameraRuntimeTests
    {
        /// <summary>本用例创建并需要销毁的场景根对象。</summary>
        private readonly List<GameObject> roots = new();
        /// <summary>本用例拥有的临时材质，结束时统一释放。</summary>
        private readonly List<Material> materials = new();
        /// <summary>测试前带 MainCamera 标签的物体，结束时恢复它们的标签。</summary>
        private readonly List<GameObject> previousMainCameras = new();
        /// <summary>访问流动控制器受保护进度的反射属性缓存。</summary>
        private static readonly PropertyInfo ProgressProperty = typeof(EffectControllerBase)
            .GetProperty("EffectAmount", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>销毁本用例拥有的对象并恢复修改过的全局设置，隔离后续测试。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 逆序销毁本用例创建的根节点，避免遗留相机或 Canvas。
            for (var i = roots.Count - 1; i >= 0; --i) if (roots[i]) Object.Destroy(roots[i]);
            // 清空“roots”，防止上一步数据影响后续观察。
            roots.Clear();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 销毁本用例拥有的材质，释放其原生资源。
            foreach (var material in materials) if (material) Object.Destroy(material);
            // 清空待清理材质集合（materials），防止上一步数据影响后续观察。
            materials.Clear();
            // 恢复原有相机的 MainCamera 标签，归还测试前的场景环境。
            foreach (var camera in previousMainCameras) if (camera) camera.tag = "MainCamera";
            // 清空“previousMainCameras”，防止上一步数据影响后续观察。
            previousMainCameras.Clear();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
        }

        /// <summary>验证世界空间 Canvas 使用主相机回退时与显式指定同一相机一致。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator WorldSpace_MainCameraFallback_ShouldMatchSameExplicitCamera()
        {
            // 创建“camera”并配置测试所需的层级或表现组件。
            var camera = CreateCamera(true);
            // 创建画布（canvas）并配置测试所需的层级或表现组件。
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup(canvas.transform, out var image);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“group.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            group.GetComponent<UIBase>().Show();
            // 将“group.EndScreenPoint”设为“camera.WorldToScreenPoint(new Vector3(3, 1, 0))”，建立当前测试条件。
            group.EndScreenPoint = camera.WorldToScreenPoint(new Vector3(3, 1, 0));
            // 保存“implicitCameraVertices”的基准值“Sample(group, image, 0.5f)”，供后续比较或恢复。
            var implicitCameraVertices = Sample(group, image, 0.5f);

            // 将“canvas.worldCamera”设为“camera”，建立当前测试条件。
            canvas.worldCamera = camera;
            // 保存“explicitCameraVertices”的基准值“Sample(group, image, 0.5f)”，供后续比较或恢复。
            var explicitCameraVertices = Sample(group, image, 0.5f);
            // 逐个比较实际顶点与基准顶点，允许浮点计算误差。
            AssertVerticesEqual(implicitCameraVertices, explicitCameraVertices);
        }

        /// <summary>验证运行时切换 Canvas 相机后生成的流动网格与新建组一致。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator WorldSpace_RuntimeCameraChange_ShouldMatchFreshGroup()
        {
            // 创建“main”并配置测试所需的层级或表现组件。
            var main = CreateCamera(true);
            // 创建“other”并配置测试所需的层级或表现组件。
            var other = CreateCamera(false);
            // 将“other.transform.position”设为“new Vector3(2, -1, -10)”，建立当前测试条件。
            other.transform.position = new Vector3(2, -1, -10);
            // 创建画布（canvas）并配置测试所需的层级或表现组件。
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            // 将“canvas.worldCamera”设为“main”，建立当前测试条件。
            canvas.worldCamera = main;
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup(canvas.transform, out var image);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“group.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            group.GetComponent<UIBase>().Show();
            // 将“group.EndScreenPoint”设为“new Vector3(470, 300, 0)”，建立当前测试条件。
            group.EndScreenPoint = new Vector3(470, 300, 0);
            // 设置流动进度并重建 Graphic，返回可比较的实际网格顶点。
            Sample(group, image, 0.5f);

            // 将“canvas.worldCamera”设为“other”，建立当前测试条件。
            canvas.worldCamera = other;
            // 创建“fresh”并配置测试所需的层级或表现组件。
            var fresh = CreateGroup(canvas.transform, out var freshImage);
            // 提交“fresh.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            fresh.GetComponent<UIBase>().Show();
            // 将“fresh.EndScreenPoint”设为“group.EndScreenPoint”，建立当前测试条件。
            fresh.EndScreenPoint = group.EndScreenPoint;
            // 保存预期结果（expected）的基准值“Sample(fresh, freshImage, 0.5f)”，供后续比较或恢复。
            var expected = Sample(fresh, freshImage, 0.5f);
            // 保存实际结果（actual）的基准值“Sample(group, image, 0.5f)”，供后续比较或恢复。
            var actual = Sample(group, image, 0.5f);
            // 逐个比较实际顶点与基准顶点，允许浮点计算误差。
            AssertVerticesEqual(expected, actual);
        }

        /// <summary>验证嵌套 Canvas 的组路径和成员网格共同使用根 Canvas 的相机。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator NestedCanvas_ShouldUseRootCanvasCameraForGroupAndMesh()
        {
            // 创建“camera”并配置测试所需的层级或表现组件。
            var camera = CreateCamera(true);
            // 创建画布（canvas）并配置测试所需的层级或表现组件。
            var canvas = CreateCanvas(RenderMode.WorldSpace);
            // 将“canvas.worldCamera”设为“camera”，建立当前测试条件。
            canvas.worldCamera = camera;
            // 创建“direct”并配置测试所需的层级或表现组件。
            var direct = CreateGroup(canvas.transform, out var directImage);
            // 创建“nestedRoot”并配置测试所需的层级或表现组件。
            var nestedRoot = CreateRect("NestedCanvas", canvas.transform);
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            nestedRoot.gameObject.AddComponent<Canvas>().overrideSorting = true;
            // 创建“nested”并配置测试所需的层级或表现组件。
            var nested = CreateGroup(nestedRoot, out var nestedImage);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“direct.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            direct.GetComponent<UIBase>().Show();
            // 提交“nested.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            nested.GetComponent<UIBase>().Show();
            // 将“direct.EndScreenPoint”设为“camera.WorldToScreenPoint(new Vector3(3, 1, 0))”，建立当前测试条件。
            direct.EndScreenPoint = camera.WorldToScreenPoint(new Vector3(3, 1, 0));
            // 将“nested.EndScreenPoint”设为“direct.EndScreenPoint”，建立当前测试条件。
            nested.EndScreenPoint = direct.EndScreenPoint;
            // 逐个比较实际顶点与基准顶点，允许浮点计算误差。
            AssertVerticesEqual(Sample(direct, directImage, 0.5f), Sample(nested, nestedImage, 0.5f));
        }

        /// <summary>验证 Overlay Canvas 坐标计算忽略显式相机和主相机。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Overlay_ShouldIgnoreAssignedAndMainCameras()
        {
            // 创建“camera”并配置测试所需的层级或表现组件。
            var camera = CreateCamera(true);
            // 创建画布（canvas）并配置测试所需的层级或表现组件。
            var canvas = CreateCanvas(RenderMode.ScreenSpaceOverlay);
            // 将“canvas.worldCamera”设为“camera”，建立当前测试条件。
            canvas.worldCamera = camera;
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup(canvas.transform, out var image);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“group.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            group.GetComponent<UIBase>().Show();
            // 将“group.EndScreenPoint”设为“new Vector3(470, 300, 0)”，建立当前测试条件。
            group.EndScreenPoint = new Vector3(470, 300, 0);
            // 验证流动网格的终点与当前 Canvas 相机转换出的目标屏幕点一致。
            AssertScreenEndpoint(group, image, null);
        }

        /// <summary>验证未指定相机的 Screen Space Camera Canvas 使用叠加坐标规则。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ScreenSpaceCameraWithoutAssignedCamera_ShouldUseOverlayCoordinates()
        {
            // 创建流动坐标测试使用的相机，可选择标记为主相机。
            CreateCamera(true);
            // 创建画布（canvas）并配置测试所需的层级或表现组件。
            var canvas = CreateCanvas(RenderMode.ScreenSpaceCamera);
            // 将“canvas.worldCamera”设为空引用，建立当前测试条件。
            canvas.worldCamera = null;
            // 创建效果所属组（group）并配置测试所需的层级或表现组件。
            var group = CreateGroup(canvas.transform, out var image);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 提交“group.GetComponent<UIBase>()”的显示请求，触发实际生命周期。
            group.GetComponent<UIBase>().Show();
            // 将“group.EndScreenPoint”设为“new Vector3(470, 300, 0)”，建立当前测试条件。
            group.EndScreenPoint = new Vector3(470, 300, 0);
            // 验证流动网格的终点与当前 Canvas 相机转换出的目标屏幕点一致。
            AssertScreenEndpoint(group, image, null);
        }

        /// <summary>创建流动坐标测试使用的相机，可选择标记为主相机。</summary>
        /// <param name="makeMain">是否将新相机标记为 MainCamera。</param>
        /// <returns>配置完成的测试相机。</returns>
        private Camera CreateCamera(bool makeMain)
        {
            // 根据“makeMain”选择对应测试路径。
            if (makeMain)
                // 在截止时间内持续观察任务状态，避免测试无限等待。
                while (Camera.main)
                {
                    // 保存“previous”的基准值“Camera.main.gameObject”，供后续比较或恢复。
                    var previous = Camera.main.gameObject;
                    // 将本轮对象或观测值加入“previousMainCameras”，供统一清理或顺序断言使用。
                    previousMainCameras.Add(previous);
                    // 将“previous.tag”设为“"Untagged"”，建立当前测试条件。
                    previous.tag = "Untagged";
                }
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("FlowCamera", typeof(Camera));
            // 将本轮对象或观测值加入“roots”，供统一清理或顺序断言使用。
            roots.Add(go);
            // 根据“makeMain”选择对应测试路径。
            if (makeMain) go.tag = "MainCamera";
            // 获取“camera”的实际组件引用，供表现与生命周期断言使用。
            var camera = go.GetComponent<Camera>();
            // 将“camera.orthographic”设为true，建立当前测试条件。
            camera.orthographic = true;
            // 将“camera.orthographicSize”设为5f，建立当前测试条件。
            camera.orthographicSize = 5f;
            // 将“camera.transform.position”设为“new Vector3(0, 0, -10)”，建立当前测试条件。
            camera.transform.position = new Vector3(0, 0, -10);
            // 将“camera.pixelRect”设为“new Rect(0, 0, 640, 480)”，建立当前测试条件。
            camera.pixelRect = new Rect(0, 0, 640, 480);
            // 根据“makeMain”选择对应测试路径。
            if (makeMain) Assert.That(Camera.main, Is.SameAs(camera));
            // 返回“camera”，交给调用方继续配置或断言。
            return camera;
        }

        /// <summary>创建指定渲染模式的测试 Canvas 并登记清理所有权。</summary>
        /// <param name="mode">测试 Canvas 使用的渲染模式。</param>
        /// <returns>指定渲染模式的测试 Canvas。</returns>
        private Canvas CreateCanvas(RenderMode mode)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("FlowCanvas", typeof(RectTransform), typeof(Canvas));
            // 将本轮对象或观测值加入“roots”，供统一清理或顺序断言使用。
            roots.Add(go);
            // 获取画布（canvas）的实际组件引用，供表现与生命周期断言使用。
            var canvas = go.GetComponent<Canvas>();
            // 将“canvas.renderMode”设为“mode”，建立当前测试条件。
            canvas.renderMode = mode;
            // 根据“mode == RenderMode.WorldSpace”选择对应测试路径。
            if (mode == RenderMode.WorldSpace) ((RectTransform)go.transform).sizeDelta = new Vector2(10, 10);
            // 返回画布（canvas），交给调用方继续配置或断言。
            return canvas;
        }

        /// <summary>创建测试流动组及必要的层级节点，供材质、路径与生命周期测试使用。</summary>
        /// <param name="parent">新对象的父节点或输出的根界面。</param>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        /// <returns>创建的流动组控制器。</returns>
        private UIFlowGroupController CreateGroup(Transform parent, out Image image)
        {
            // 使用独立普通父节点，避免无关 UIBase 之间的互斥影响坐标测试。
            var container = CreateRect("GroupContainer", parent);
            // 添加并保存效果所属组（group），供后续生命周期与效果检查使用。
            var group = CreateRect("FlowGroup", container).gameObject.AddComponent<UIFlowGroupController>();
            // 创建矩形变换（rect）并配置测试所需的层级或表现组件。
            var rect = CreateRect("FlowImage", group.transform);
            // 将“rect.sizeDelta”设为“Vector2.one”，建立当前测试条件。
            rect.sizeDelta = Vector2.one;
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            image = rect.gameObject.AddComponent<Image>();
            // 创建测试材质（material），隔离本用例的资源或观测数据。
            var material = new Material(Shader.Find("UI/Default"));
            // 将本轮对象或观测值加入待清理材质集合（materials），供统一清理或顺序断言使用。
            materials.Add(material);
            // 将成员图像（image）的当前材质设为测试材质（material），建立当前测试条件。
            image.material = material;
            // 添加测试需要的组件，供后续生命周期与效果检查使用。
            image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 返回效果所属组（group），交给调用方继续配置或断言。
            return group;
        }

        /// <summary>创建指定父节点下的 RectTransform，建立可控的测试层级。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="parent">新对象的父节点或输出的根界面。</param>
        /// <returns>指定层级下新建的 RectTransform。</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject(name, typeof(RectTransform));
            // 将新节点挂到指定父节点，并保持本地变换。
            go.transform.SetParent(parent, false);
            // 返回“(RectTransform)go.transform”，交给调用方继续配置或断言。
            return (RectTransform)go.transform;
        }

        /// <summary>设置流动进度并重建 Graphic，返回可比较的实际网格顶点。</summary>
        /// <param name="group">需要配置或验证的流动组控制器。</param>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        /// <param name="progress">需要采样的归一化播放进度。</param>
        /// <returns>当前采样得到的顶点数组。</returns>
        private static Vector3[] Sample(UIFlowGroupController group, Image image, float progress)
        {
            // 确认“ProgressProperty”存在。
            Assert.NotNull(ProgressProperty);
            // 只设置时间轴进度，几何仍通过真实 Graphic 管线重建。
            ProgressProperty.SetValue(group, progress);
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存实际网格（mesh）的基准值“image.canvasRenderer.GetMesh()”，供后续比较或恢复。
            var mesh = image.canvasRenderer.GetMesh();
            // 确认实际网格（mesh）存在。
            Assert.NotNull(mesh);
            // 确认“mesh.vertexCount”满足约束“Is.GreaterThan(0)”。
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            // 返回“mesh.vertices”，交给调用方继续配置或断言。
            return mesh.vertices;
        }

        /// <summary>验证流动网格的终点与当前 Canvas 相机转换出的目标屏幕点一致。</summary>
        /// <param name="group">需要配置或验证的流动组控制器。</param>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        /// <param name="camera">屏幕与世界坐标换算采用的相机。</param>
        private static void AssertScreenEndpoint(UIFlowGroupController group, Image image, Camera camera)
        {
            // 确认“RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, group.EndScreenPoint, …”为真。
            Assert.IsTrue(RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,
                group.EndScreenPoint, camera, out var expected));
            // 保存顶点位置（vertices）的基准值“Sample(group, image, 1f)”，供后续比较或恢复。
            var vertices = Sample(group, image, 1f);
            // 逐项检查或处理“var vertex in vertices”，覆盖集合中的每个对象。
            foreach (var vertex in vertices)
                // 确认“Vector2.Distance(expected, vertex)”满足约束“Is.LessThan(0.001f)”。
                Assert.That(Vector2.Distance(expected, vertex), Is.LessThan(0.001f));
        }

        /// <summary>逐个比较实际顶点与基准顶点，允许浮点计算误差。</summary>
        /// <param name="expected">本轮需要满足的基准结果。</param>
        /// <param name="actual">本轮实际生成或读取的结果。</param>
        private static void AssertVerticesEqual(Vector3[] expected, Vector3[] actual)
        {
            // 确认实际结果（actual）的元素数量等于预期结果（expected）的元素数量。
            Assert.AreEqual(expected.Length, actual.Length);
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = 0; i < expected.Length; ++i)
                // 确认“Vector3.Distance(expected[i], actual[i])”满足约束“Is.LessThan(0.001f)”。
                Assert.That(Vector3.Distance(expected[i], actual[i]), Is.LessThan(0.001f));
        }
    }
}
