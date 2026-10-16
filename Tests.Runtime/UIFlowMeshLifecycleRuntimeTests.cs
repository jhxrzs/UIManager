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
    public class UIFlowMeshLifecycleRuntimeTests
    {
        private readonly List<Object> ownedObjects = new();
        private static readonly FieldInfo TMPSettingsInstance = typeof(TMP_Settings)
            .GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
        private object previousTMPSettings;
        private bool replacedTMPSettings;

        [TearDown]
        public void TearDown()
        {
            try
            {
                for (var i = ownedObjects.Count - 1; i >= 0; i--)
                    if (ownedObjects[i]) Object.DestroyImmediate(ownedObjects[i]);
            }
            finally
            {
                ownedObjects.Clear();
                if (replacedTMPSettings) TMPSettingsInstance.SetValue(null, previousTMPSettings);
                previousTMPSettings = null;
                replacedTMPSettings = false;
            }
        }

        [TestCase(0.5f, false)]
        [TestCase(0.5f, true)]
        [TestCase(1f, false)]
        [TestCase(1f, true)]
        public void TMP_RemoveFlow_ShouldRestoreOriginalMesh(float amount, bool destroy)
        {
            var text = CreateText();
            text.Rebuild(CanvasUpdate.PreRender);
            var original = CaptureText(text);
            Assert.That(original.Vertices.Length, Is.EqualTo(4), "前置条件：真实TMP管线已生成单字形网格。");
            Assert.That(original.Colors[0].a, Is.EqualTo(255));

            var effect = text.gameObject.AddComponent<UIFlowMeshEffect>();
            effect.EffectAmount = amount;
            text.Rebuild(CanvasUpdate.PreRender);
            var deformed = CaptureText(text);
            Assert.That(deformed.Vertices, Is.Not.EqualTo(original.Vertices), "前置条件：效果确实修改了文字顶点。");
            if (amount == 1f)
                foreach (var color in deformed.Colors) Assert.That(color.a, Is.Zero);
            Assert.IsFalse(text.havePropertiesChanged, "完成重建后不能遗留属性变化标记来掩盖停用缺陷。");

            if (destroy) Object.DestroyImmediate(effect);
            else effect.enabled = false;

            // ForceMeshUpdate会主动标记属性变化，无法检出OnDisable没有请求文字重建的问题。
            text.Rebuild(CanvasUpdate.PreRender);
            AssertMeshEqual(original, CaptureText(text));

            if (!destroy)
            {
                // 池化节点重新激活时，仍被停用的效果不得再次改变文字。
                text.gameObject.SetActive(false);
                text.gameObject.SetActive(true);
                text.Rebuild(CanvasUpdate.PreRender);
                AssertMeshEqual(original, CaptureText(text));
                effect.enabled = true;
                text.Rebuild(CanvasUpdate.PreRender);
                Assert.That(effect.EffectAmount, Is.EqualTo(amount), "停用不应重置动效进度。");
                AssertMeshEqual(deformed, CaptureText(text));
                effect.enabled = false;
                text.Rebuild(CanvasUpdate.PreRender);
                AssertMeshEqual(original, CaptureText(text));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Image_RemoveFlow_ShouldRestoreOriginalMesh(bool destroy)
        {
            var image = CreateGraphicObject("FlowImage").AddComponent<Image>();
            // 显式材质让测试不依赖可选的流动Shader资源，也不触发缺少模板的警告。
            image.material = Own(new Material(Shader.Find("UI/Default")));
            image.Rebuild(CanvasUpdate.PreRender);
            var original = CaptureImage(image);
            Assert.That(original.Vertices.Length, Is.EqualTo(4));

            var effect = image.gameObject.AddComponent<UIFlowMeshEffect>();
            effect.EffectAmount = 1f;
            image.Rebuild(CanvasUpdate.PreRender);
            var deformed = CaptureImage(image);
            Assert.That(deformed.Vertices.Length, Is.GreaterThan(0));
            foreach (var color in deformed.Colors) Assert.That(color.a, Is.Zero);

            if (destroy) Object.DestroyImmediate(effect);
            else effect.enabled = false;
            image.Rebuild(CanvasUpdate.PreRender);
            AssertMeshEqual(original, CaptureImage(image));
        }

        private TextMeshProUGUI CreateText()
        {
            // 不访问TMP_Settings.instance getter，避免无Essentials工程弹出导入窗口。
            Assert.NotNull(TMPSettingsInstance);
            previousTMPSettings = TMPSettingsInstance.GetValue(null);
            var settings = Own(ScriptableObject.CreateInstance<TMP_Settings>());
            TMPSettingsInstance.SetValue(null, settings);
            replacedTMPSettings = true;
            TMP_Settings.fontFeatures.Clear();
            SetField(settings, "m_linebreakingRules", new TMP_Settings.LineBreakingTable
            {
                leadingCharacters = new HashSet<uint>(),
                followingCharacters = new HashSet<uint>()
            });

            // 静态内存字形只负责生成可观察的几何，不依赖OS字体、FontEngine或TMP的SDF资源。
            var font = Own(ScriptableObject.CreateInstance<TMP_FontAsset>());
            font.name = "FlowLifecycleTestFont";
            SetField(font, "m_Version", "1.1.0");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.faceInfo = new FaceInfo
            {
                familyName = "FlowLifecycleTest", styleName = "Regular", pointSize = 16,
                scale = 1, lineHeight = 16, ascentLine = 12, capLine = 12,
                meanLine = 8, descentLine = -4
            };
            SetField(font, "m_AtlasWidth", 16);
            SetField(font, "m_AtlasHeight", 16);
            // TMP_FontAsset.OnDestroy负责清理字体拥有的图集和材质。
            font.atlasTextures = new[] { new Texture2D(16, 16) };
            font.material = new Material(Shader.Find("UI/Default")) { mainTexture = font.atlasTextures[0] };
            var glyph = new Glyph(1, new GlyphMetrics(8, 12, 0, 12, 8), new GlyphRect(0, 0, 8, 12), 1, 0);
            font.glyphTable.Add(glyph);
            foreach (var character in new uint[] { 'A', '_', 0x2026, ' ' })
                font.characterTable.Add(new TMP_Character(character, font, glyph));
            font.ReadFontAssetDefinition();
            TMP_Settings.defaultFontAsset = font;

            var text = CreateGraphicObject("FlowText").AddComponent<TextMeshProUGUI>();
            text.fontSize = 16;
            text.text = "A";
            text.color = Color.white;
            return text;
        }

        private GameObject CreateGraphicObject(string name)
        {
            var root = Own(new GameObject(name + "Canvas", typeof(RectTransform), typeof(Canvas)));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(root.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(160, 40);
            return go;
        }

        private T Own<T>(T value) where T : Object
        {
            ownedObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new InvalidOperationException("Missing TMP fixture field: " + name);
        }

        private static MeshSnapshot CaptureText(TextMeshProUGUI text)
        {
            var info = text.textInfo.meshInfo[0];
            var vertices = new Vector3[info.vertexCount];
            var colors = new Color32[info.vertexCount];
            Array.Copy(info.vertices, vertices, vertices.Length);
            Array.Copy(info.colors32, colors, colors.Length);
            return new MeshSnapshot(vertices, colors);
        }

        private static MeshSnapshot CaptureImage(Image image)
        {
            var mesh = image.canvasRenderer.GetMesh();
            Assert.NotNull(mesh, "前置条件：CanvasRenderer已收到Image网格。");
            return new MeshSnapshot(mesh.vertices, mesh.colors32);
        }

        private static void AssertMeshEqual(MeshSnapshot expected, MeshSnapshot actual)
        {
            CollectionAssert.AreEqual(expected.Vertices, actual.Vertices, "顶点应恢复到对应的原始或动效状态。");
            CollectionAssert.AreEqual(expected.Colors, actual.Colors, "顶点颜色及透明度应完整恢复。");
        }

        private readonly struct MeshSnapshot
        {
            public readonly Vector3[] Vertices;
            public readonly Color32[] Colors;
            public MeshSnapshot(Vector3[] vertices, Color32[] colors)
            {
                Vertices = vertices;
                Colors = colors;
            }
        }
    }
}
