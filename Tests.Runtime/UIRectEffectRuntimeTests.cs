using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private static readonly EffectSerializedRectTransform RectShownPose = new()
        {
            anchorPos = Vector2.zero, angle = Vector3.zero, scale = Vector3.one,
            sizeDelta = new Vector2(100, 200), pivot = new Vector2(0.25f, 0.25f)
        };

        private static readonly EffectSerializedRectTransform RectHiddenPose = new()
        {
            anchorPos = new Vector2(100, 200), angle = new Vector3(0, 0, 100),
            scale = new Vector3(3, 5, 1), sizeDelta = new Vector2(300, 600),
            pivot = new Vector2(0.75f, 0.75f)
        };

        private RectEffectController CreateRectEffect(out UIBase ui)
        {
            var go = new GameObject("RectEffect", typeof(RectTransform));
            _createdObjects.Add(go);
            go.transform.SetParent(CreateObject("RectEffectContainer").transform, false);
            go.SetActive(false);
            ui = go.AddComponent<UIBase>();
            ui.enabled = false;
            var effect = go.AddComponent<RectEffectController>();
            SetRectEffectField(effect, "showTarget", RectShownPose);
            SetRectEffectField(effect, "hideTarget", RectHiddenPose);
            SetRectEffectField(effect, "effectAmount", 1f);
            SetRectEffectField(effect, "showDuration", 10f);
            SetRectEffectField(effect, "hideDuration", 10f);
            // 模拟预制体经过编辑器配置后再进入 Awake，不依赖运行时补查缓存。
            ValidateRectEffect(effect);
            go.SetActive(true);
            return effect;
        }

        private static void SetRectEffectField(RectEffectController effect, string name, object value)
        {
            for (var type = typeof(RectEffectController); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(effect, value);
                return;
            }
            Assert.Fail("Missing effect configuration: " + name);
        }

        private static void ValidateRectEffect(RectEffectController effect) =>
            typeof(RectEffectController).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, null);

        private static void SampleRectAmount(RectEffectController effect, float amount) =>
            typeof(EffectControllerBase).GetProperty("EffectAmount", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(effect, amount);

        private static void SampleRectProgress(RectEffectController effect, float progress) =>
            typeof(RectEffectController).GetMethod("ApplyTransitionProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, new object[] { progress });

        private static float ReadRectAmount(RectEffectController effect) =>
            (float)typeof(EffectControllerBase).GetProperty("EffectAmount",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);

        private static void ConfigureRectParts(RectEffectController effect, bool showing)
        {
            var prefix = showing ? "show" : "hide";
            SetRectEffectField(effect, prefix + "AnchorPosEase", Ease.InQuad);
            SetRectEffectField(effect, prefix + "AngleEase", Ease.OutQuad);
            SetRectEffectField(effect, prefix + "ScaleEase", Ease.InCubic);
            SetRectEffectField(effect, prefix + "SizeDeltaEase", Ease.OutCubic);
            SetRectEffectField(effect, prefix + "PivotEase", Ease.Linear);
        }

        private static void AssertRectPose(RectTransform rect, EffectSerializedRectTransform expected)
        {
            Assert.That(Vector2.Distance(expected.anchorPos, rect.anchoredPosition), Is.LessThan(0.001f), "position");
            Assert.That(Quaternion.Angle(Quaternion.Euler(expected.angle), rect.localRotation), Is.LessThan(0.01f), "rotation");
            Assert.That(Vector3.Distance(expected.scale, rect.localScale), Is.LessThan(0.001f), "scale");
            Assert.That(Vector2.Distance(expected.sizeDelta, rect.sizeDelta), Is.LessThan(0.001f), "size");
            Assert.That(Vector2.Distance(expected.pivot, rect.pivot), Is.LessThan(0.001f), "pivot");
        }

        [UnityTest]
        public IEnumerator RectEffect_Hide_ShouldUseAllFiveHideCurves() => VerifyRectParts(false);

        [UnityTest]
        public IEnumerator RectEffect_Show_ShouldUseAllFiveShowCurvesInPlaybackDirection() => VerifyRectParts(true);

        private IEnumerator VerifyRectParts(bool showing)
        {
            var effect = CreateRectEffect(out var ui);
            ConfigureRectParts(effect, showing);
            if (showing) ui.Hide();
            else ui.Show();
            using var cancellation = new CancellationTokenSource();
            var operation = CaptureException(showing ? ui.ShowAsync(cancellation.Token) : ui.HideAsync(cancellation.Token));
            try
            {
                Assert.IsFalse(operation.IsCompleted);
                // 采样真实运行中的控制器，直接设置精确进度，避免按帧计时导致半程断言波动。
                SampleRectProgress(effect, 0.5f);
                var expected = new EffectSerializedRectTransform
                {
                    anchorPos = showing ? new Vector2(75, 150) : new Vector2(25, 50),
                    angle = new Vector3(0, 0, showing ? 25 : 75),
                    scale = showing ? new Vector3(2.75f, 4.5f, 1) : new Vector3(1.25f, 1.5f, 1),
                    sizeDelta = showing ? new Vector2(125, 250) : new Vector2(275, 550),
                    pivot = new Vector2(0.5f, 0.5f)
                };
                AssertRectPose((RectTransform)effect.transform, expected);
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        [UnityTest]
        public IEnumerator RectEffect_GlobalHideCurve_ShouldOverridePartsAndApplyOnlyOnce() => VerifyRectGlobal(false);

        [UnityTest]
        public IEnumerator RectEffect_GlobalShowCurve_ShouldOverridePartsAndApplyOnlyOnce() => VerifyRectGlobal(true);

        private IEnumerator VerifyRectGlobal(bool showing)
        {
            var effect = CreateRectEffect(out var ui);
            ConfigureRectParts(effect, showing);
            SetRectEffectField(effect, showing ? "showEase" : "hideEase", Ease.InQuad);
            ValidateRectEffect(effect);
            if (showing) ui.Hide();
            else ui.Show();
            using var cancellation = new CancellationTokenSource();
            var operation = CaptureException(showing ? ui.ShowAsync(cancellation.Token) : ui.HideAsync(cancellation.Token));
            try
            {
                // 从线性半程进入真实运行路径，全局 InQuad 映射为 0.25，分项不能再次求曲线。
                SampleRectProgress(effect, 0.5f);
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x,
                    Is.EqualTo(showing ? 75f : 25f).Within(0.001f));
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        [UnityTest]
        public IEnumerator RectEffect_ReversingDuringHide_ShouldStartFromCurrentPoseAndKeepNewCurve()
        {
            var effect = CreateRectEffect(out var ui);
            SetRectEffectField(effect, "hideAnchorPosEase", Ease.InQuad);
            SetRectEffectField(effect, "showAnchorPosEase", Ease.OutQuad);
            ui.Show();
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            SampleRectProgress(effect, 0.5f);
            Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(25f).Within(0.001f));
            using var cancellation = new CancellationTokenSource();
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            try
            {
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(25f).Within(0.001f),
                    "反向播放的第一帧不能跳回另一条曲线上的位置。");
                yield return WaitForTask(hiding);
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
                // 旧动画 finally 已运行，新动画的半程仍应从 25 按 OutQuad 前进到 0，即 6.25。
                SampleRectProgress(effect, 0.5f);
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(6.25f).Within(0.001f));
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(showing);
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
        }

        [UnityTest]
        public IEnumerator RectEffect_SyncShowReplacingHide_ShouldApplyExactEndpoint()
        {
            var effect = CreateRectEffect(out var ui);
            ConfigureRectParts(effect, false);
            ui.Show();
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            SampleRectProgress(effect, 0.5f);

            ui.Show();

            AssertRectPose((RectTransform)effect.transform, RectShownPose);
            yield return WaitForTask(hiding);
            Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
            ui.Hide();
            AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
        }

        private static void ConfigureRectUniformEase(RectEffectController effect, bool showing, Ease ease, bool global)
        {
            var prefix = showing ? "show" : "hide";
            SetRectEffectField(effect, prefix + "Ease", global ? ease : Ease.NotSet);
            foreach (var part in new[] { "AnchorPos", "Angle", "Scale", "SizeDelta", "Pivot" })
                SetRectEffectField(effect, prefix + part + "Ease", global ? Ease.Linear : ease);
        }

        private static EffectSerializedRectTransform ExpectedRectPose(
            EffectSerializedRectTransform from, EffectSerializedRectTransform to, float progress) => new()
        {
            anchorPos = from.anchorPos + (to.anchorPos - from.anchorPos) * progress,
            angle = from.angle + (to.angle - from.angle) * progress,
            scale = from.scale + (to.scale - from.scale) * progress,
            sizeDelta = from.sizeDelta + (to.sizeDelta - from.sizeDelta) * progress,
            pivot = from.pivot + (to.pivot - from.pivot) * progress
        };

        [UnityTest]
        public IEnumerator RectEffect_HideBackAndElastic_ShouldPreserveOvershootForAllProperties() =>
            VerifyRectOvershoot(false);

        [UnityTest]
        public IEnumerator RectEffect_ShowBackAndElastic_ShouldPreserveOvershootForAllProperties() =>
            VerifyRectOvershoot(true);

        private IEnumerator VerifyRectOvershoot(bool showing)
        {
            var curves = new[] { Ease.InBack, Ease.OutBack, Ease.InElastic, Ease.OutElastic };
            // 已知曲线半程值独立于生产求值器，既检出截断，也检出重复应用曲线。
            var halfProgress = new[] { -0.0876975f, 1.0876975f, -0.015625f, 1.015625f };
            for (var i = 0; i < curves.Length; i++)
            {
                var global = CreateRectEffect(out var globalUI);
                var parts = CreateRectEffect(out var partsUI);
                ConfigureRectUniformEase(global, showing, curves[i], true);
                ConfigureRectUniformEase(parts, showing, curves[i], false);
                if (showing) { globalUI.Hide(); partsUI.Hide(); }
                else { globalUI.Show(); partsUI.Show(); }

                using var globalCancellation = new CancellationTokenSource();
                using var partsCancellation = new CancellationTokenSource();
                var globalOperation = CaptureException(showing
                    ? globalUI.ShowAsync(globalCancellation.Token) : globalUI.HideAsync(globalCancellation.Token));
                var partsOperation = CaptureException(showing
                    ? partsUI.ShowAsync(partsCancellation.Token) : partsUI.HideAsync(partsCancellation.Token));
                try
                {
                    Assert.IsFalse(globalOperation.IsCompleted);
                    Assert.IsFalse(partsOperation.IsCompleted);
                    SampleRectProgress(global, 0.5f);
                    SampleRectProgress(parts, 0.5f);
                    var expected = ExpectedRectPose(showing ? RectHiddenPose : RectShownPose,
                        showing ? RectShownPose : RectHiddenPose, halfProgress[i]);
                    AssertRectPose((RectTransform)global.transform, expected);
                    AssertRectPose((RectTransform)parts.transform, expected);
                    AssertRectPose((RectTransform)global.transform,
                        EffectSerializedRectTransform.GetInstance((RectTransform)parts.transform));
                    Assert.That(ReadRectAmount(global), Is.InRange(0f, 1f), "表现允许过冲，标准进度仍保持有效范围。");
                    Assert.That(ReadRectAmount(parts), Is.InRange(0f, 1f));
                }
                finally
                {
                    globalCancellation.Cancel();
                    partsCancellation.Cancel();
                }
                yield return WaitForTask(globalOperation);
                yield return WaitForTask(partsOperation);
                Assert.That(globalOperation.Result, Is.InstanceOf<OperationCanceledException>());
                Assert.That(partsOperation.Result, Is.InstanceOf<OperationCanceledException>());
            }
        }

        [UnityTest]
        public IEnumerator RectEffect_ReversingDuringOvershoot_ShouldPreservePoseAndNewTransition()
        {
            var effect = CreateRectEffect(out var ui);
            ConfigureRectUniformEase(effect, false, Ease.OutBack, true);
            ConfigureRectUniformEase(effect, true, Ease.OutElastic, true);
            ui.Show();
            using var hideCancellation = new CancellationTokenSource();
            using var showCancellation = new CancellationTokenSource();
            var hiding = CaptureException(ui.HideAsync(hideCancellation.Token));
            System.Threading.Tasks.Task<Exception> showing = null;
            try
            {
                SampleRectProgress(effect, 0.5f);
                var rect = (RectTransform)effect.transform;
                AssertRectPose(rect, ExpectedRectPose(RectShownPose, RectHiddenPose, 1.0876975f));
                var overshoot = EffectSerializedRectTransform.GetInstance(rect);
                showing = CaptureException(ui.ShowAsync(showCancellation.Token));
                AssertRectPose(rect, overshoot);
                yield return WaitForTask(hiding);
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());

                // 旧动画取消收尾之后，新动画仍从捕获的越界姿态独立求值。
                SampleRectProgress(effect, 0.5f);
                AssertRectPose(rect, ExpectedRectPose(overshoot, RectShownPose, 1.015625f));
                Assert.IsFalse(showing.IsCompleted);
            }
            finally
            {
                hideCancellation.Cancel();
                showCancellation.Cancel();
            }
            yield return WaitForTask(showing);
            Assert.That(showing.Result, Is.InstanceOf<OperationCanceledException>());
        }

        [UnityTest]
        public IEnumerator RectEffect_CanceledOvershootRetry_ShouldRunFullDurationFromActualPose()
        {
            foreach (var global in new[] { true, false })
            {
                var effect = CreateRectEffect(out var ui);
                ConfigureRectUniformEase(effect, false, Ease.OutBack, global);
                ui.Show();
                using var cancellation = new CancellationTokenSource();
                var hiding = CaptureException(ui.HideAsync(cancellation.Token));
                EffectSerializedRectTransform overshoot;
                try
                {
                    SampleRectProgress(effect, 0.5f);
                    overshoot = EffectSerializedRectTransform.GetInstance((RectTransform)effect.transform);
                    AssertRectPose((RectTransform)effect.transform,
                        ExpectedRectPose(RectShownPose, RectHiddenPose, 1.0876975f));
                    if (global) Assert.That(ReadRectAmount(effect), Is.EqualTo(1f));
                }
                finally { cancellation.Cancel(); }
                yield return WaitForTask(hiding);
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
                AssertRectPose((RectTransform)effect.transform, overshoot);

                const float retryDuration = 0.2f;
                SetRectEffectField(effect, "hideDuration", retryDuration);
                using var retryCancellation = new CancellationTokenSource();
                var startedAt = Time.unscaledTimeAsDouble;
                var retry = CaptureException(ui.HideAsync(retryCancellation.Token));
                try
                {
                    Assert.IsFalse(retry.IsCompleted);
                    AssertRectPose((RectTransform)effect.transform, overshoot);
                    // 即使全局标准进度已等于隐藏终点，实际姿态仍需要完整的下一段过渡。
                    SampleRectProgress(effect, 0.5f);
                    AssertRectPose((RectTransform)effect.transform,
                        ExpectedRectPose(overshoot, RectHiddenPose, 1.0876975f));
                    Assert.IsFalse(retry.IsCompleted);
                    yield return WaitForTask(retry);
                    Assert.IsNull(retry.Result);
                    Assert.That(Time.unscaledTimeAsDouble - startedAt,
                        Is.GreaterThanOrEqualTo(retryDuration - 0.00001), "重试必须按完整配置时长播放。");
                    AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
                    Assert.That(ReadRectAmount(effect), Is.EqualTo(1f));
                    Assert.AreEqual(UIState.Hided, ui.State);
                }
                finally { retryCancellation.Cancel(); }
            }
        }

        [Test]
        public void RectEffect_OnValidate_ShouldPreservePartsAndPreviewLinearly()
        {
            var effect = CreateRectEffect(out _);
            ConfigureRectParts(effect, true);
            ConfigureRectParts(effect, false);
            SetRectEffectField(effect, "showEase", Ease.OutCubic);
            SetRectEffectField(effect, "hideEase", Ease.InCubic);
            SampleRectAmount(effect, 0.5f);
            ValidateRectEffect(effect);
            foreach (var prefix in new[] { "show", "hide" })
            {
                var fields = new[] { "AnchorPosEase", "AngleEase", "ScaleEase", "SizeDeltaEase", "PivotEase" };
                var expected = new[] { Ease.InQuad, Ease.OutQuad, Ease.InCubic, Ease.OutCubic, Ease.Linear };
                for (var i = 0; i < fields.Length; i++)
                    Assert.AreEqual(expected[i], typeof(RectEffectController)
                        .GetField(prefix + fields[i], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect));
            }
            Assert.That(((RectTransform)effect.transform).anchoredPosition.x, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void RectEffect_ZeroDuration_ShouldApplyEndpointsAndRaycastState()
        {
            var effect = CreateRectEffect(out var ui);
            SetRectEffectField(effect, "showDuration", 0f);
            SetRectEffectField(effect, "hideDuration", 0f);
            ConfigureRectParts(effect, true);
            ConfigureRectParts(effect, false);
            var showing = CaptureException(ui.ShowAsync(CancellationToken.None));
            Assert.IsTrue(showing.IsCompleted);
            Assert.IsNull(showing.Result);
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
            Assert.IsTrue(effect.GetComponent<CanvasGroup>().blocksRaycasts);
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            Assert.IsTrue(hiding.IsCompleted);
            Assert.IsNull(hiding.Result);
            AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
            Assert.IsFalse(effect.GetComponent<CanvasGroup>().blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator RectEffect_PlaybackWithParts_ShouldMatchCurveAndReachEndpoints() => VerifyRectPlayback(false);

        [UnityTest]
        public IEnumerator RectEffect_PlaybackWithGlobal_ShouldMatchCurveAndReachEndpoints() => VerifyRectPlayback(true);

        private IEnumerator VerifyRectPlayback(bool useGlobal)
        {
            var effect = CreateRectEffect(out var ui);
            SetRectEffectField(effect, "showDuration", 0.1f);
            SetRectEffectField(effect, "hideDuration", 0.1f);
            SetRectEffectField(effect, "showAnchorPosEase", Ease.OutQuad);
            SetRectEffectField(effect, "hideAnchorPosEase", Ease.InQuad);
            if (useGlobal)
            {
                SetRectEffectField(effect, "showEase", Ease.OutQuad);
                SetRectEffectField(effect, "hideEase", Ease.InQuad);
            }
            ui.Show();
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            var deadline = Time.realtimeSinceStartup + TestTimeout;
            while (!hiding.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                var amount = ReadRectAmount(effect);
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x,
                    Is.EqualTo(100f * (useGlobal ? amount : amount * amount)).Within(0.001f));
                yield return null;
            }
            yield return WaitForTask(hiding);
            Assert.IsNull(hiding.Result);
            AssertRectPose((RectTransform)effect.transform, RectHiddenPose);
            var showing = CaptureException(ui.ShowAsync(CancellationToken.None));
            deadline = Time.realtimeSinceStartup + TestTimeout;
            while (!showing.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                var amount = ReadRectAmount(effect);
                Assert.That(((RectTransform)effect.transform).anchoredPosition.x,
                    Is.EqualTo(100f * (useGlobal ? amount : amount * amount)).Within(0.001f));
                yield return null;
            }
            yield return WaitForTask(showing);
            Assert.IsNull(showing.Result);
            AssertRectPose((RectTransform)effect.transform, RectShownPose);
        }
    }
}
