using System;
using System.Collections;
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
        [UnityTest]
        public IEnumerator Effect_DestroyDuringRectShow_ShouldOnlyFinishRemovedEffect() =>
            VerifyRemovedEffect(typeof(RectEffectController), true);

        [UnityTest]
        public IEnumerator Effect_DestroyDuringRectHide_ShouldOnlyFinishRemovedEffect() =>
            VerifyRemovedEffect(typeof(RectEffectController), false);

        [UnityTest]
        public IEnumerator Effect_DestroyDuringAlphaShow_ShouldStopWriting() =>
            VerifyRemovedEffect(typeof(AlphaEffectController), true);

        [UnityTest]
        public IEnumerator Effect_DestroyDuringColorHide_ShouldStopWriting() =>
            VerifyRemovedEffect(typeof(ColorEffectController), false);

        [UnityTest]
        public IEnumerator Effect_DestroyDuringAnimationShow_ShouldStopSampling() =>
            VerifyRemovedEffect(typeof(AnimController), true);

        private IEnumerator VerifyRemovedEffect(Type type, bool showing)
        {
            var go = CreateRuntimeEffectObject("EffectLifetime", out var ui);
            var graphic = type == typeof(ColorEffectController) ? go.AddComponent<Image>() : null;
            var effect = (EffectControllerBase)go.AddComponent(type);
            SetEffectField(effect, "showDuration", 60f);
            SetEffectField(effect, "hideDuration", 60f);
            if (effect is RectEffectController rectEffect)
            {
                SetRectEffectField(rectEffect, "showTarget", RectShownPose);
                SetRectEffectField(rectEffect, "hideTarget", RectHiddenPose);
            }
            go.SetActive(true);
            AnimationClip clip = null;
            var pending = new UniTaskCompletionSource();
            try
            {
                if (type == typeof(AnimController))
                {
                    clip = new AnimationClip { legacy = true };
                    clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 20));
                    var animation = go.GetComponent<Animation>();
                    animation.AddClip(clip, "LifetimeClip");
                    animation.clip = clip;
                }
                ui.Show();
                if (showing) ui.Hide();
                if (showing) ui.ShowAsyncActions += _ => pending.Task;
                else ui.HideAsyncActions += _ => pending.Task;
                var operation = CaptureException(showing
                    ? ui.ShowAsync(CancellationToken.None) : ui.HideAsync(CancellationToken.None));
                yield return null;
                var pose = EffectSerializedRectTransform.GetInstance((RectTransform)go.transform);
                var alpha = go.GetComponent<CanvasGroup>().alpha;
                var color = graphic ? graphic.color : Color.white;
                Object.DestroyImmediate(effect);
                yield return null;
                yield return null;

                AssertRectPose((RectTransform)go.transform, pose);
                Assert.AreEqual(alpha, go.GetComponent<CanvasGroup>().alpha);
                if (graphic) Assert.AreEqual(color, graphic.color);
                Assert.IsFalse(operation.IsCompleted, "移除一个动效不能提前完成其他仍在等待的行为。");
                pending.TrySetResult();
                yield return WaitForTask(operation);
                Assert.IsNull(operation.Result, "单独移除动效不得让面板操作失败或取消。");
                Assert.AreEqual(showing ? UIState.Showed : UIState.Hided, ui.State);
            }
            finally
            {
                pending.TrySetResult();
                if (effect) Object.DestroyImmediate(effect);
                if (clip) Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        public IEnumerator Effect_DestroyAndCallerCancel_ShouldStillCancelPanel()
        {
            foreach (var cancelFirst in new[] { false, true })
            {
                var effect = CreateRectEffect(out var ui);
                using var cancellation = new CancellationTokenSource();
                var operation = CaptureException(ui.ShowAsync(cancellation.Token));
                if (cancelFirst) cancellation.Cancel();
                Object.DestroyImmediate(effect);
                if (!cancelFirst) cancellation.Cancel();
                yield return WaitForTask(operation);
                Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
                Assert.AreEqual(UIState.Showing, ui.State);
            }
        }

        [UnityTest]
        public IEnumerator Effect_DestroyEntirePanel_ShouldStillCancelOperation()
        {
            var effect = CreateRectEffect(out var ui);
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
            Object.DestroyImmediate(effect.gameObject);
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Effect_DestroyedBeforeItsSnapshotCallback_ShouldBeSkipped(bool asynchronous)
        {
            var go = CreateRuntimeEffectObject("SnapshotEffect", out var ui);
            EffectControllerBase effect = null;
            ui.ShowActions += () => Object.DestroyImmediate(effect);
            ui.ShowAsyncActions += _ =>
            {
                Object.DestroyImmediate(effect);
                return UniTask.CompletedTask;
            };
            effect = go.AddComponent<RectEffectController>();
            go.SetActive(true);
            if (asynchronous)
            {
                var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
                Assert.IsTrue(operation.IsCompleted);
                Assert.IsNull(operation.Result);
            }
            else ui.Show();
            Assert.AreEqual(UIState.Showed, ui.State);
        }
    }
}
