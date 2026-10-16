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
        public IEnumerator Effect_LateAttachment_ShouldSynchronizeHiddenPresentation() =>
            VerifyLateAttachmentStableState(false);

        [UnityTest]
        public IEnumerator Effect_LateAttachment_ShouldSynchronizeShownPresentation() =>
            VerifyLateAttachmentStableState(true);

        private IEnumerator VerifyLateAttachmentStableState(bool shown)
        {
            foreach (var type in new[] { typeof(AlphaEffectController), typeof(ColorEffectController),
                         typeof(RectEffectController), typeof(AnimController) })
            {
                var go = CreateRuntimeEffectObject("Late" + type.Name, out var ui);
                var graphic = type == typeof(ColorEffectController) ? go.AddComponent<Image>() : null;
                go.SetActive(true);
                ui.Show();
                if (!shown) ui.Hide();
                var stateChanges = 0;
                var afterCalls = 0;
                ui.OnStateChanged += (_, _) => stateChanges++;
                ui.AfterUIShowedEvent += () => afterCalls++;
                ui.AfterUIHidedEvent += () => afterCalls++;
                var effect = (EffectControllerBase)go.AddComponent(type);
                AnimationClip clip = null;
                try
                {
                    if (effect is RectEffectController rect)
                    {
                        SetRectEffectField(rect, "showTarget", RectShownPose);
                        SetRectEffectField(rect, "hideTarget", RectHiddenPose);
                    }
                    if (type == typeof(AnimController))
                    {
                        clip = new AnimationClip { legacy = true };
                        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 20));
                        var animation = go.GetComponent<Animation>();
                        animation.AddClip(clip, "LateClip");
                        animation.clip = clip;
                        go.transform.localPosition = new Vector3(7f, 0f, 0f);
                    }
                    yield return null; // Real Start, after caller configuration and derived Awake.
                    Assert.That((float)ReadEffectField(effect, "effectAmount"), Is.EqualTo(shown ? 0f : 1f));
                    if (type == typeof(AlphaEffectController))
                        Assert.That(go.GetComponent<CanvasGroup>().alpha, Is.EqualTo(shown ? 1f : 0f));
                    if (graphic) Assert.That(graphic.color, Is.EqualTo(shown ? Color.white : Color.clear));
                    if (effect is RectEffectController)
                        AssertRectPose((RectTransform)go.transform, shown ? RectShownPose : RectHiddenPose);
                    if (type == typeof(AnimController))
                        Assert.That(go.transform.localPosition.x, Is.EqualTo(shown ? 20f : 0f).Within(0.001f));
                    Assert.AreEqual(shown ? UIState.Showed : UIState.Hided, ui.State);
                    Assert.AreEqual(0, stateChanges, "Only the newly attached effect may be synchronized.");
                    Assert.AreEqual(0, afterCalls, "Attaching an effect must not replay UI notifications.");
                }
                finally
                {
                    if (effect) Object.DestroyImmediate(effect);
                    if (clip) Object.DestroyImmediate(clip);
                }
            }
        }

        [UnityTest]
        public IEnumerator Effect_LateAttachment_DuringTransition_ShouldWaitForStableState()
        {
            foreach (var showing in new[] { false, true })
            {
                var go = CreateRuntimeEffectObject("AttachDuringTransition", out var ui);
                go.SetActive(true);
                ui.Show();
                if (showing) ui.Hide();
                var pending = new UniTaskCompletionSource();
                if (showing) ui.ShowAsyncActions += _ => pending.Task;
                else ui.HideAsyncActions += _ => pending.Task;
                var operation = CaptureException(showing
                    ? ui.ShowAsync(CancellationToken.None) : ui.HideAsync(CancellationToken.None));
                var effect = go.AddComponent<AlphaEffectController>();
                var group = go.GetComponent<CanvasGroup>();
                group.alpha = 0.42f;
                try
                {
                    yield return null;
                    Assert.IsFalse(operation.IsCompleted);
                    Assert.AreEqual(0.42f, group.alpha, "Late attachment must not snap an existing transition.");
                    pending.TrySetResult();
                    yield return WaitForTask(operation);
                    Assert.IsNull(operation.Result);
                    Assert.AreEqual(showing ? 1f : 0f, group.alpha);
                }
                finally { pending.TrySetResult(); }
            }
        }

        [UnityTest]
        public IEnumerator Effect_LateAttachment_NormalActionBeforeStart_ShouldKeepItsTransition()
        {
            var go = CreateRuntimeEffectObject("ActionBeforeEffectStart", out var ui);
            go.SetActive(true);
            ui.Show();
            var effect = go.AddComponent<AlphaEffectController>();
            SetEffectField(effect, "hideDuration", 60f);
            using var cancellation = new CancellationTokenSource();
            var operation = CaptureException(ui.HideAsync(cancellation.Token));
            try
            {
                yield return null;
                Assert.IsFalse(operation.IsCompleted);
                Assert.Greater(go.GetComponent<CanvasGroup>().alpha, 0.5f,
                    "First Start must not replace the effect's already running transition with its endpoint.");
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(operation);
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
        }

        [UnityTest]
        public IEnumerator Effect_StartBeforeUIInitialization_ShouldWaitForNormalInitialAction()
        {
            var go = CreateRuntimeEffectObject("EffectBeforeUIStart", out var ui);
            var effect = go.AddComponent<LateAttachmentAwakeProbe>();
            go.SetActive(true);
            yield return null; // UI stays disabled and has not initialized its serialized state.
            Assert.IsFalse(ui.IsUIStateInitialized);
            Assert.AreEqual(0, effect.ActionCount);
            ui.enabled = true;
            yield return null;
            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.AreEqual(1, effect.ActionCount, "UI initial Start must apply its action exactly once.");
            Assert.IsFalse(effect.AppliedBeforeAwakeFinished);
        }

        [UnityTest]
        public IEnumerator Effect_LateAttachment_ShouldWaitUntilDerivedAwakeFinished()
        {
            var go = CreateRuntimeEffectObject("LateAwakeProbe", out var ui);
            go.SetActive(true);
            ui.Show(); ui.Hide();
            var effect = go.AddComponent<LateAttachmentAwakeProbe>();
            Assert.AreEqual(0, effect.ActionCount);
            yield return null;
            Assert.AreEqual(1, effect.ActionCount);
            Assert.IsFalse(effect.AppliedBeforeAwakeFinished);
        }
    }

    public sealed class LateAttachmentAwakeProbe : EffectControllerBase
    {
        public int ActionCount { get; private set; }
        public bool AppliedBeforeAwakeFinished { get; private set; }
        private bool _awakeFinished;

        protected override void Awake()
        {
            base.Awake();
            _awakeFinished = true;
        }

        protected override void OnUIShow()
        {
            AppliedBeforeAwakeFinished |= !_awakeFinished;
            ActionCount++;
            base.OnUIShow();
        }

        protected override void OnUIHide()
        {
            AppliedBeforeAwakeFinished |= !_awakeFinished;
            ActionCount++;
            base.OnUIHide();
        }
    }
}
