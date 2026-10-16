using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    // PlayMode用例在Unity主线程执行；此夹具在每个用例结束时完整恢复TMP的静态设置。
    /// <summary>验证移除或禁用流动效果后的 Image 与 TMP 原始网格恢复。</summary>
    public partial class UIFlowMeshLifecycleRuntimeTests
    {
        /// <summary>由当前测试拥有并负责销毁的 Unity 对象集合。</summary>
        private readonly List<Object> ownedObjects = new();
        /// <summary>访问 TMP 全局设置实例的反射字段缓存。</summary>
        private static readonly FieldInfo TMPSettingsInstance = typeof(TMP_Settings)
            .GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
        /// <summary>测试替换前的 TMP 全局设置实例。</summary>
        private object previousTMPSettings;
        /// <summary>本用例是否替换过 TMP 静态设置，决定清理时是否恢复。</summary>
        private bool replacedTMPSettings;

        /// <summary>销毁本用例拥有的对象并恢复修改过的全局设置，隔离后续测试。</summary>
        [TearDown]
        public void TearDown()
        {
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 按既定范围逐项采样或处理，检查边界及重复执行行为。
                for (var i = ownedObjects.Count - 1; i >= 0; i--)
                    // 根据“ownedObjects[i]”选择对应测试路径。
                    if (ownedObjects[i]) Object.DestroyImmediate(ownedObjects[i]);
            }
            finally
            {
                // 清空待清理对象集合（ownedObjects），防止上一步数据影响后续观察。
                ownedObjects.Clear();
                // 根据TMP 设置替换标记（replacedTMPSettings）选择对应测试路径。
                if (replacedTMPSettings) TMPSettingsInstance.SetValue(null, previousTMPSettings);
                // 将原 TMP 全局设置（previousTMPSettings）设为空引用，建立当前测试条件。
                previousTMPSettings = null;
                // 将TMP 设置替换标记（replacedTMPSettings）设为false，建立当前测试条件。
                replacedTMPSettings = false;
            }
        }

        /// <summary>验证禁用或移除 TMP 流动效果后恢复原始文字网格，重新启用仍保留动效进度。</summary>
        /// <param name="amount">需要采样的动效效果量。</param>
        /// <param name="destroy">是否销毁效果组件；为 false 时仅禁用组件。</param>
        [TestCase(0.5f, false)]
        [TestCase(0.5f, true)]
        [TestCase(1f, false)]
        [TestCase(1f, true)]
        public void TMP_RemoveFlow_ShouldRestoreOriginalMesh(float amount, bool destroy)
        {
            // 创建文字组件（text）并配置测试所需的层级或表现组件。
            var text = CreateText();
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            text.Rebuild(CanvasUpdate.PreRender);
            // 保存原始基准（original）的基准值“CaptureText(text)”，供后续比较或恢复。
            var original = CaptureText(text);
            // 确认“original.Vertices”的元素数量符合4。
            Assert.That(original.Vertices.Length, Is.EqualTo(4), "前置条件：真实TMP管线已生成单字形网格。");
            // 确认“original.Colors[0].a”符合255。
            Assert.That(original.Colors[0].a, Is.EqualTo(255));

            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = text.gameObject.AddComponent<UIFlowMeshEffect>();
            // 将动效组件（effect）的流动进度设为效果量（amount），建立当前测试条件。
            effect.EffectAmount = amount;
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            text.Rebuild(CanvasUpdate.PreRender);
            // 保存“deformed”的基准值“CaptureText(text)”，供后续比较或恢复。
            var deformed = CaptureText(text);
            // 确认“deformed.Vertices”不同于“original.Vertices”。
            Assert.That(deformed.Vertices, Is.Not.EqualTo(original.Vertices), "前置条件：效果确实修改了文字顶点。");
            // 根据“amount == 1f”选择对应测试路径。
            if (amount == 1f)
                // 逐项检查或处理“var color in deformed.Colors”，覆盖集合中的每个对象。
                foreach (var color in deformed.Colors) Assert.That(color.a, Is.Zero);
            // 确认“text.havePropertiesChanged”为假。
            Assert.IsFalse(text.havePropertiesChanged, "完成重建后不能遗留属性变化标记来掩盖停用缺陷。");

            // 根据“destroy”选择对应测试路径。
            if (destroy) Object.DestroyImmediate(effect);
            // 覆盖条件不成立时的另一条配置或清理路径。
            else effect.enabled = false;

            // ForceMeshUpdate会主动标记属性变化，无法检出OnDisable没有请求文字重建的问题。
            text.Rebuild(CanvasUpdate.PreRender);
            // 验证网格的顶点和颜色完整恢复到指定快照。
            AssertMeshEqual(original, CaptureText(text));

            // 根据“!destroy”选择对应测试路径。
            if (!destroy)
            {
                // 池化节点重新激活时，仍被停用的效果不得再次改变文字。
                text.gameObject.SetActive(false);
                // 切换“text.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
                text.gameObject.SetActive(true);
                // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
                text.Rebuild(CanvasUpdate.PreRender);
                // 验证网格的顶点和颜色完整恢复到指定快照。
                AssertMeshEqual(original, CaptureText(text));
                // 将动效组件（effect）的组件启用状态设为true，建立当前测试条件。
                effect.enabled = true;
                // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
                text.Rebuild(CanvasUpdate.PreRender);
                // 确认动效组件（effect）的流动进度符合效果量（amount）。
                Assert.That(effect.EffectAmount, Is.EqualTo(amount), "停用不应重置动效进度。");
                // 验证网格的顶点和颜色完整恢复到指定快照。
                AssertMeshEqual(deformed, CaptureText(text));
                // 将动效组件（effect）的组件启用状态设为false，建立当前测试条件。
                effect.enabled = false;
                // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
                text.Rebuild(CanvasUpdate.PreRender);
                // 验证网格的顶点和颜色完整恢复到指定快照。
                AssertMeshEqual(original, CaptureText(text));
            }
        }

        /// <summary>验证禁用或移除 Image 流动效果后恢复原始顶点与颜色。</summary>
        /// <param name="destroy">是否销毁效果组件；为 false 时仅禁用组件。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void Image_RemoveFlow_ShouldRestoreOriginalMesh(bool destroy)
        {
            // 添加并保存成员图像（image），供后续生命周期与效果检查使用。
            var image = CreateGraphicObject("FlowImage").AddComponent<Image>();
            // 显式材质让测试不依赖可选的流动Shader资源，也不触发缺少模板的警告。
            image.material = Own(new Material(Shader.Find("UI/Default")));
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存原始基准（original）的基准值“CaptureImage(image)”，供后续比较或恢复。
            var original = CaptureImage(image);
            // 确认“original.Vertices”的元素数量符合4。
            Assert.That(original.Vertices.Length, Is.EqualTo(4));

            // 添加并保存动效组件（effect），供后续生命周期与效果检查使用。
            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            // 将动效组件（effect）的流动进度设为1f，建立当前测试条件。
            effect.EffectAmount = 1f;
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 保存“deformed”的基准值“CaptureImage(image)”，供后续比较或恢复。
            var deformed = CaptureImage(image);
            // 确认“deformed.Vertices”的元素数量满足约束“Is.GreaterThan(0)”。
            Assert.That(deformed.Vertices.Length, Is.GreaterThan(0));
            // 逐项检查或处理“var color in deformed.Colors”，覆盖集合中的每个对象。
            foreach (var color in deformed.Colors) Assert.That(color.a, Is.Zero);

            // 根据“destroy”选择对应测试路径。
            if (destroy) Object.DestroyImmediate(effect);
            // 覆盖条件不成立时的另一条配置或清理路径。
            else effect.enabled = false;
            // 执行真实 Canvas 网格重建，检查实际顶点表现而非仅依赖进度值。
            image.Rebuild(CanvasUpdate.PreRender);
            // 验证网格的顶点和颜色完整恢复到指定快照。
            AssertMeshEqual(original, CaptureImage(image));
        }

        /// <summary>构造不依赖 TMP Essentials 的内存字体与文字组件，提供可观察的真实文字网格。</summary>
        /// <returns>具备内存字体的 TMP 文字组件。</returns>
        private TextMeshProUGUI CreateText()
        {
            // 不访问TMP_Settings.instance getter，避免无Essentials工程弹出导入窗口。
            Assert.NotNull(TMPSettingsInstance);
            // 将原 TMP 全局设置（previousTMPSettings）设为“TMPSettingsInstance.GetValue(null)”，建立当前测试条件。
            previousTMPSettings = TMPSettingsInstance.GetValue(null);
            // 保存临时设置（settings）的基准值“Own(ScriptableObject.CreateInstance<TMP_Settings>())”，供后续比较或恢复。
            var settings = Own(ScriptableObject.CreateInstance<TMP_Settings>());
            // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
            TMPSettingsInstance.SetValue(null, settings);
            // 将TMP 设置替换标记（replacedTMPSettings）设为true，建立当前测试条件。
            replacedTMPSettings = true;
            // 清空“TMP_Settings.fontFeatures”，防止上一步数据影响后续观察。
            TMP_Settings.fontFeatures.Clear();
            // 沿继承链写入测试所需的非公开字段，避免扩大生产代码接口。
            SetField(settings, "m_linebreakingRules", new TMP_Settings.LineBreakingTable
            {
                leadingCharacters = new HashSet<uint>(),
                followingCharacters = new HashSet<uint>()
            });

            // 静态内存字形只负责生成可观察的几何，不依赖OS字体、FontEngine或TMP的SDF资源。
            var font = Own(ScriptableObject.CreateInstance<TMP_FontAsset>());
            // 将“font”的对象名称设为“"FlowLifecycleTestFont"”，建立当前测试条件。
            font.name = "FlowLifecycleTestFont";
            // 沿继承链写入测试所需的非公开字段，避免扩大生产代码接口。
            SetField(font, "m_Version", "1.1.0");
            // 将“font.atlasPopulationMode”设为“AtlasPopulationMode.Static”，建立当前测试条件。
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            // 将“font.faceInfo”设为“new FaceInfo {”，建立当前测试条件。
            font.faceInfo = new FaceInfo
            {
                familyName = "FlowLifecycleTest", styleName = "Regular", pointSize = 16,
                scale = 1, lineHeight = 16, ascentLine = 12, capLine = 12,
                meanLine = 8, descentLine = -4
            };
            // 沿继承链写入测试所需的非公开字段，避免扩大生产代码接口。
            SetField(font, "m_AtlasWidth", 16);
            // 沿继承链写入测试所需的非公开字段，避免扩大生产代码接口。
            SetField(font, "m_AtlasHeight", 16);
            // TMP_FontAsset.OnDestroy负责清理字体拥有的图集和材质。
            font.atlasTextures = new[] { new Texture2D(16, 16) };
            // 将“font”的当前材质设为“new Material(Shader.Find("UI/Default")) { mainTexture = font.atlasTextures[0] }”，建立当前测试条件。
            font.material = new Material(Shader.Find("UI/Default")) { mainTexture = font.atlasTextures[0] };
            // 创建“glyph”，隔离本用例的资源或观测数据。
            var glyph = new Glyph(1, new GlyphMetrics(8, 12, 0, 12, 8), new GlyphRect(0, 0, 8, 12), 1, 0);
            // 将本轮对象或观测值加入“font.glyphTable”，供统一清理或顺序断言使用。
            font.glyphTable.Add(glyph);
            // 逐项检查或处理“var character in new uint[] { 'A', '_', 0x2026, ' ' }”，覆盖集合中的每个对象。
            foreach (var character in new uint[] { 'A', '_', 0x2026, ' ' })
                // 将本轮对象或观测值加入“font.characterTable”，供统一清理或顺序断言使用。
                font.characterTable.Add(new TMP_Character(character, font, glyph));
            // 刷新内存字体的字符与字形索引，使真实 TMP 管线能够生成测试网格。
            font.ReadFontAssetDefinition();
            // 将“TMP_Settings.defaultFontAsset”设为“font”，建立当前测试条件。
            TMP_Settings.defaultFontAsset = font;

            // 添加并保存文字组件（text），供后续生命周期与效果检查使用。
            var text = CreateGraphicObject("FlowText").AddComponent<TextMeshProUGUI>();
            // 将“text.fontSize”设为16，建立当前测试条件。
            text.fontSize = 16;
            // 将“text.text”设为“"A"”，建立当前测试条件。
            text.text = "A";
            // 将文字组件（text）的颜色设为“Color.white”，建立当前测试条件。
            text.color = Color.white;
            // 返回文字组件（text），交给调用方继续配置或断言。
            return text;
        }

        /// <summary>创建带 Canvas 和 CanvasRenderer 的 Graphic 宿主，并登记测试清理。</summary>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <returns>包含必要渲染组件的 Graphic 宿主。</returns>
        private GameObject CreateGraphicObject(string name)
        {
            // 保存根节点（root）的基准值“Own(new GameObject(name + "Canvas", typeof(RectTransform), typeof(Canvas)))”，供后续比较或恢复。
            var root = Own(new GameObject(name + "Canvas", typeof(RectTransform), typeof(Canvas)));
            // 将“root.GetComponent<Canvas>().renderMode”设为“RenderMode.ScreenSpaceOverlay”，建立当前测试条件。
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            // 创建宿主物体（go），隔离本用例的资源或观测数据。
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            go.transform.SetParent(root.transform, false);
            // 将“((RectTransform)go.transform).sizeDelta”设为“new Vector2(160, 40)”，建立当前测试条件。
            ((RectTransform)go.transform).sizeDelta = new Vector2(160, 40);
            // 返回宿主物体（go），交给调用方继续配置或断言。
            return go;
        }

        /// <summary>登记测试创建的 Unity 对象，以便结束时统一释放。</summary>
        /// <typeparam name="T">要保存或配置的目标对象类型。</typeparam>
        /// <param name="value">写入配置或用于断言的测试值。</param>
        /// <returns>传入的同一个对象，便于继续配置。</returns>
        private T Own<T>(T value) where T : Object
        {
            // 将本轮对象或观测值加入待清理对象集合（ownedObjects），供统一清理或顺序断言使用。
            ownedObjects.Add(value);
            // 返回“value”，交给调用方继续配置或断言。
            return value;
        }

        /// <summary>沿继承链写入测试所需的非公开字段，避免扩大生产代码接口。</summary>
        /// <param name="target">需要绑定或通过反射操作的目标对象。</param>
        /// <param name="name">对象或目标字段的名称。</param>
        /// <param name="value">写入配置或用于断言的测试值。</param>
        private static void SetField(object target, string name, object value)
        {
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                // 取得“field”的反射入口，仅在测试内访问非公开配置。
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                // 根据“field == null”选择对应测试路径。
                if (field == null) continue;
                // 通过反射写入指定序列化配置，构造与 Inspector 设置一致的测试条件。
                field.SetValue(target, value);
                // 结束当前辅助流程，避免继续处理已完成分支。
                return;
            }
            // 抛出预设异常，验证调用方的失败传播与收尾行为。
            throw new InvalidOperationException("Missing TMP fixture field: " + name);
        }

        /// <summary>复制 TMP 当前有效顶点与颜色，保留不受后续重建影响的网格快照。</summary>
        /// <param name="text">需要读取当前网格的 TMP 文字组件。</param>
        /// <returns>文字当前有效顶点与颜色的独立快照。</returns>
        private static MeshSnapshot CaptureText(TextMeshProUGUI text)
        {
            // 保存“info”的基准值“text.textInfo.meshInfo[0]”，供后续比较或恢复。
            var info = text.textInfo.meshInfo[0];
            // 创建顶点位置（vertices），隔离本用例的资源或观测数据。
            var vertices = new Vector3[info.vertexCount];
            // 创建顶点颜色（colors），隔离本用例的资源或观测数据。
            var colors = new Color32[info.vertexCount];
            // 复制当前网格数据，防止后续重建覆盖用于比较的基准快照。
            Array.Copy(info.vertices, vertices, vertices.Length);
            // 复制当前网格数据，防止后续重建覆盖用于比较的基准快照。
            Array.Copy(info.colors32, colors, colors.Length);
            // 返回“new MeshSnapshot(vertices, colors)”，交给调用方继续配置或断言。
            return new MeshSnapshot(vertices, colors);
        }

        /// <summary>读取 Image 已提交至 CanvasRenderer 的网格，保存顶点与颜色快照。</summary>
        /// <param name="image">用于读取真实 Canvas 网格的 Image。</param>
        /// <returns>Image 当前顶点与颜色的网格快照。</returns>
        private static MeshSnapshot CaptureImage(Image image)
        {
            // 保存实际网格（mesh）的基准值“image.canvasRenderer.GetMesh()”，供后续比较或恢复。
            var mesh = image.canvasRenderer.GetMesh();
            // 确认实际网格（mesh）存在。
            Assert.NotNull(mesh, "前置条件：CanvasRenderer已收到Image网格。");
            // 返回“new MeshSnapshot(mesh.vertices, mesh.colors32)”，交给调用方继续配置或断言。
            return new MeshSnapshot(mesh.vertices, mesh.colors32);
        }

        /// <summary>验证网格的顶点和颜色完整恢复到指定快照。</summary>
        /// <param name="expected">本轮需要满足的基准结果。</param>
        /// <param name="actual">本轮实际生成或读取的结果。</param>
        private static void AssertMeshEqual(MeshSnapshot expected, MeshSnapshot actual)
        {
            // 确认“actual.Vertices”等于“expected.Vertices”。
            CollectionAssert.AreEqual(expected.Vertices, actual.Vertices, "顶点应恢复到对应的原始或动效状态。");
            // 确认“actual.Colors”等于“expected.Colors”。
            CollectionAssert.AreEqual(expected.Colors, actual.Colors, "顶点颜色及透明度应完整恢复。");
        }

    }
}
