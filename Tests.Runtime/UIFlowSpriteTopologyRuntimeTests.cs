using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>验证流动变形对紧密 Sprite 拓扑和规则矩形细分的处理。</summary>
    public class UIFlowSpriteTopologyRuntimeTests
    {
        /// <summary>隔离本用例 UI 层级的 Canvas 根对象。</summary>
        private GameObject canvasRoot;
        /// <summary>本用例生成并需要释放的纹理、Sprite 或材质资源。</summary>
        private readonly List<Object> assets = new();

        /// <summary>销毁本用例拥有的对象并恢复修改过的全局设置，隔离后续测试。</summary>
        [TearDown]
        public void TearDown()
        {
            // 根据画布根节点（canvasRoot）选择对应测试路径。
            if (canvasRoot) Object.DestroyImmediate(canvasRoot);
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = assets.Count - 1; i >= 0; --i)
                // 根据“assets[i]”选择对应测试路径。
                if (assets[i]) Object.DestroyImmediate(assets[i]);
            // 清空待清理资源集合（assets），防止上一步数据影响后续观察。
            assets.Clear();
        }

        /// <summary>验证紧密四顶点 Sprite 在流动变形后保留原始拓扑和 UV。</summary>
        /// <param name="progress">需要采样的归一化播放进度。</param>
        [TestCase(0.000001f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void TightFourVertexSprite_ShouldPreserveItsOriginalTopology(float progress)
        {
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateImage();
            // 将“image.sprite”设为“CreateDiamondSprite()”，建立当前测试条件。
            image.sprite = CreateDiamondSprite();
            // 将“image.useSpriteMesh”设为true，建立当前测试条件。
            image.useSpriteMesh = true;
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存原始基准（original）的基准值“image.canvasRenderer.GetMesh()”，供后续比较或恢复。
            var original = image.canvasRenderer.GetMesh();
            // 保存“originalBounds”的基准值“original.bounds”，供后续比较或恢复。
            var originalBounds = original.bounds;
            // 保存“originalTriangles”的基准值“original.triangles”，供后续比较或恢复。
            var originalTriangles = original.triangles;
            // 确认“original.vertexCount”符合4。
            Assert.That(original.vertexCount, Is.EqualTo(4));
            // 确认“originalBounds.size.x”符合100f，允许指定的浮点误差。
            Assert.That(originalBounds.size.x, Is.EqualTo(100f).Within(0.01f));
            // 确认“originalBounds.size.y”符合100f，允许指定的浮点误差。
            Assert.That(originalBounds.size.y, Is.EqualTo(100f).Within(0.01f));

            // 将“effect.EndScreenPoint”设为“new Vector3(600, 300, 0)”，建立当前测试条件。
            effect.EndScreenPoint = new Vector3(600, 300, 0);
            // 将动效组件（effect）的流动进度设为播放进度（progress），建立当前测试条件。
            effect.EffectAmount = progress;
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存实际结果（actual）的基准值“image.canvasRenderer.GetMesh()”，供后续比较或恢复。
            var actual = image.canvasRenderer.GetMesh();
            // 确认“actual.vertexCount”符合4。
            Assert.That(actual.vertexCount, Is.EqualTo(4));
            // 确认“actual.triangles”等于“originalTriangles”。
            CollectionAssert.AreEqual(originalTriangles, actual.triangles);
            // 根据“progress < 0.00001f”选择对应测试路径。
            if (progress < 0.00001f)
            {
                // 确认“Vector3.Distance(originalBounds.min, actual.bounds.min)”满足约束“Is.LessThan(0.01f)”。
                Assert.That(Vector3.Distance(originalBounds.min, actual.bounds.min), Is.LessThan(0.01f));
                // 确认“Vector3.Distance(originalBounds.max, actual.bounds.max)”满足约束“Is.LessThan(0.01f)”。
                Assert.That(Vector3.Distance(originalBounds.max, actual.bounds.max), Is.LessThan(0.01f));
            }
            // 根据“progress == 1f”选择对应测试路径。
            if (progress == 1f)
                // 逐项检查或处理“var color in actual.colors32”，覆盖集合中的每个对象。
                foreach (var color in actual.colors32) Assert.That(color.a, Is.Zero);
        }

        /// <summary>验证规则矩形 Simple Image 仍使用细分网格执行流动变形。</summary>
        /// <param name="hasSprite">测试 Image 是否配置 Sprite。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void RectangularSimpleImage_ShouldStillUseSubdividedMesh(bool hasSprite)
        {
            // 创建成员图像（image）并配置测试所需的层级或表现组件。
            var image = CreateImage();
            // 根据“hasSprite”选择对应测试路径。
            if (hasSprite) image.sprite = CreateDiamondSprite();
            // 未启用 Sprite Mesh 时，即使 Sprite 有自定义几何，Image 也按矩形绘制。
            image.useSpriteMesh = false;
            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 将“effect.EndScreenPoint”设为“new Vector3(600, 300, 0)”，建立当前测试条件。
            effect.EndScreenPoint = new Vector3(600, 300, 0);
            // 将动效组件（effect）的流动进度设为0.5f，建立当前测试条件。
            effect.EffectAmount = 0.5f;
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 确认“image.canvasRenderer.GetMesh().vertexCount”满足约束“Is.GreaterThan(4)”。
            Assert.That(image.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(4));
        }

        /// <summary>创建可重建网格的测试 Image，供流动效果的顶点与可见性断言使用。</summary>
        /// <returns>用于真实网格重建的测试 Image。</returns>
        private Image CreateImage()
        {
            // 将画布根节点（canvasRoot）设为“new GameObject("SpriteFlowCanvas", typeof(RectTransform), typeof(Canvas))”，建立当前测试条件。
            canvasRoot = new GameObject("SpriteFlowCanvas", typeof(RectTransform), typeof(Canvas));
            // 将“canvasRoot.GetComponent<Canvas>().renderMode”设为“RenderMode.ScreenSpaceOverlay”，建立当前测试条件。
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject("SpriteFlowImage", typeof(RectTransform), typeof(Image));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            go.transform.SetParent(canvasRoot.transform, false);
            // 获取成员图像（image）的实际组件引用，供表现与生命周期断言使用。
            var image = go.GetComponent<Image>();
            // 将“image.rectTransform.sizeDelta”设为“new Vector2(100, 100)”，建立当前测试条件。
            image.rectTransform.sizeDelta = new Vector2(100, 100);
            // 创建测试材质（material），隔离本用例的资源或观测数据。
            var material = new Material(Shader.Find("UI/Default"));
            // 将本轮对象或观测值加入待清理资源集合（assets），供统一清理或顺序断言使用。
            assets.Add(material);
            // 将成员图像（image）的当前材质设为测试材质（material），建立当前测试条件。
            image.material = material;
            // 返回成员图像（image），交给调用方继续配置或断言。
            return image;
        }

        /// <summary>创建菱形紧密 Sprite，构造不同于规则矩形的四顶点拓扑。</summary>
        /// <returns>使用菱形紧密拓扑的 Sprite。</returns>
        private Sprite CreateDiamondSprite()
        {
            // 创建“texture”，隔离本用例的资源或观测数据。
            var texture = new Texture2D(2, 2);
            // 将本轮对象或观测值加入待清理资源集合（assets），供统一清理或顺序断言使用。
            assets.Add(texture);
            // 保存“sprite”的基准值“Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f)”，供后续比较或恢复。
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            // 将本轮对象或观测值加入待清理资源集合（assets），供统一清理或顺序断言使用。
            assets.Add(sprite);
            // OverrideGeometry 使用 Sprite Rect 内的像素坐标，尚未进行轴心与 PPU 转换。
            sprite.OverrideGeometry(new[]
            {
                new Vector2(0, 1), new Vector2(1, 2), new Vector2(2, 1), new Vector2(1, 0)
            }, new ushort[] { 0, 1, 2, 0, 2, 3 });
            // 返回“sprite”，交给调用方继续配置或断言。
            return sprite;
        }
    }
}
