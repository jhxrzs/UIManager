using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private static FieldInfo GetEffectField(Type type, string name)
        {
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            throw new InvalidOperationException("Missing effect field: " + name);
        }

        private static void SetEffectField(EffectControllerBase effect, string name, object value) =>
            GetEffectField(effect.GetType(), name).SetValue(effect, value);

        private static object ReadEffectField(EffectControllerBase effect, string name) =>
            GetEffectField(effect.GetType(), name).GetValue(effect);

        private GameObject CreateRuntimeEffectObject(string name, out UIBase ui)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _createdObjects.Add(go);
            go.transform.SetParent(CreateObject(name + "Container").transform, false);
            go.SetActive(false);
            ui = go.AddComponent<UIBase>();
            ui.enabled = false;
            return go;
        }

        [TestCase(typeof(EffectControllerBase), false)]
        [TestCase(typeof(AlphaEffectController), false)]
        [TestCase(typeof(ColorEffectController), false)]
        [TestCase(typeof(RectEffectController), false)]
        [TestCase(typeof(AnimController), false)]
        [TestCase(typeof(UIFlowGroupController), false)]
        [TestCase(typeof(EffectControllerBase), true)]
        [TestCase(typeof(AlphaEffectController), true)]
        [TestCase(typeof(ColorEffectController), true)]
        [TestCase(typeof(RectEffectController), true)]
        [TestCase(typeof(AnimController), true)]
        [TestCase(typeof(UIFlowGroupController), true)]
        public void Effect_RuntimeAdd_ShouldResolveReferencesAndSubscribe(Type type, bool activeAtAdd)
        {
            var go = CreateRuntimeEffectObject("RuntimeEffect", out var ui);
            if (type == typeof(ColorEffectController)) go.AddComponent<Image>();
            if (activeAtAdd) go.SetActive(true);
            var effect = (EffectControllerBase)go.AddComponent(type);
            if (!activeAtAdd)
            {
                // 显式清空缓存，避免测试依赖编辑器是否在 AddComponent 时调用 OnValidate。
                SetEffectField(effect, "uiBase", null);
                SetEffectField(effect, "canvasGroup", null);
                if (type == typeof(ColorEffectController)) SetEffectField(effect, "graphic", null);
                go.SetActive(true);
            }

            Assert.AreSame(ui, ReadEffectField(effect, "uiBase"));
            Assert.AreSame(go.GetComponent<CanvasGroup>(), ReadEffectField(effect, "canvasGroup"));
            ui.Show();
            ui.Hide();
            Assert.That((float)ReadEffectField(effect, "effectAmount"), Is.EqualTo(1f));
            ui.Show();
            Assert.That((float)ReadEffectField(effect, "effectAmount"), Is.EqualTo(0f));
            if (type == typeof(ColorEffectController))
                Assert.AreSame(go.GetComponent<Image>(), ReadEffectField(effect, "graphic"));
        }

        [UnityTest]
        public IEnumerator Effect_RuntimeAlpha_ShouldSupportAsyncShowAndHide()
        {
            var go = CreateRuntimeEffectObject("RuntimeAlpha", out var ui);
            go.SetActive(true);
            var effect = go.AddComponent<AlphaEffectController>();
            SetEffectField(effect, "showDuration", 0f);
            SetEffectField(effect, "hideDuration", 0f);
            ui.Show();
            var hide = CaptureException(ui.HideAsync(CancellationToken.None));
            yield return WaitForTask(hide);
            Assert.IsNull(hide.Result);
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            var show = CaptureException(ui.ShowAsync(CancellationToken.None));
            yield return WaitForTask(show);
            Assert.IsNull(show.Result);
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [Test]
        public void Effect_RuntimeReferences_ShouldPreserveExplicitConfiguration()
        {
            var go = CreateRuntimeEffectObject("ConfiguredEffect", out var ownUI);
            var target = CreateRuntimeEffectObject("ConfiguredTarget", out var targetUI);
            target.SetActive(true);
            var effect = go.AddComponent<AlphaEffectController>();
            var targetGroup = target.GetComponent<CanvasGroup>();
            SetEffectField(effect, "uiBase", targetUI);
            SetEffectField(effect, "canvasGroup", targetGroup);
            go.SetActive(true);
            Assert.AreSame(targetUI, ReadEffectField(effect, "uiBase"));
            Assert.AreSame(targetGroup, ReadEffectField(effect, "canvasGroup"));
            targetUI.Show(); targetUI.Hide();
            Assert.That(targetGroup.alpha, Is.EqualTo(0f));
            ownUI.Show();
            Assert.That(targetGroup.alpha, Is.EqualTo(0f), "不得改绑到本物体的UIBase。");
            Object.DestroyImmediate(effect);
            targetGroup.alpha = 0.4f;
            targetUI.Show();
            Assert.That(targetGroup.alpha, Is.EqualTo(0.4f), "销毁组件必须从实际订阅目标解绑。");
        }

        [Test]
        public void Effect_RuntimeColor_ShouldApplyAndCacheGraphic()
        {
            var go = CreateRuntimeEffectObject("RuntimeColor", out var ui);
            var image = go.AddComponent<Image>();
            var effect = go.AddComponent<ColorEffectController>();
            SetEffectField(effect, "graphic", null);
            go.SetActive(true);
            ui.Show();
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.AreSame(image, ReadEffectField(effect, "graphic"));
            ui.Hide();
            Assert.That(image.color, Is.EqualTo(Color.clear));
        }

        [Test]
        public void Effect_RuntimeRect_ShouldUseConfiguredTargetsWithoutOnValidate()
        {
            var go = CreateRuntimeEffectObject("RuntimeRect", out var ui);
            go.SetActive(true);
            var effect = go.AddComponent<RectEffectController>();
            SetRectEffectField(effect, "showTarget", RectShownPose);
            SetRectEffectField(effect, "hideTarget", RectHiddenPose);
            ui.Show();
            AssertRectPose((RectTransform)go.transform, RectShownPose);
            ui.Hide();
            AssertRectPose((RectTransform)go.transform, RectHiddenPose);
        }

        [Test]
        public void Effect_RuntimeAnimation_ShouldAllowClipConfigurationAfterAdding()
        {
            var go = CreateRuntimeEffectObject("RuntimeAnimation", out var ui);
            go.SetActive(true);
            go.AddComponent<AnimController>();
            var animation = go.GetComponent<Animation>();
            Assert.IsNotNull(animation);
            Assert.IsFalse(animation.playAutomatically);
            ui.Show(); ui.Hide(); // 未配置片段的过渡不能抛空引用。
            var clip = new AnimationClip { legacy = true };
            try
            {
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 20));
                animation.AddClip(clip, "RuntimeClip");
                animation.clip = clip;
                ui.Show();
                Assert.That(go.transform.localPosition.x, Is.EqualTo(20f).Within(0.001f));
                ui.Hide();
                Assert.That(go.transform.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test]
        public void Effect_DestroyedCanvasCache_ShouldResolveAndCacheLocalComponent()
        {
            var go = CreateRuntimeEffectObject("RuntimeAlpha", out var ui);
            go.SetActive(true);
            var effect = go.AddComponent<AlphaEffectController>();
            var external = CreateObject("ExternalCanvas");
            SetEffectField(effect, "canvasGroup", external.AddComponent<CanvasGroup>());
            Object.DestroyImmediate(external);
            ui.Show(); ui.Hide();
            Assert.AreSame(go.GetComponent<CanvasGroup>(), ReadEffectField(effect, "canvasGroup"));
            Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
        }
    }
}
