using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private RectEffectController CreateRotationEffect(float shownAngle, float hiddenAngle, out UIBase ui)
        {
            var effect = CreateRectEffect(out ui);
            var shown = RectShownPose;
            var hidden = RectHiddenPose;
            shown.angle = new Vector3(0f, 0f, shownAngle);
            hidden.angle = new Vector3(0f, 0f, hiddenAngle);
            SetRectEffectField(effect, "showTarget", shown);
            SetRectEffectField(effect, "hideTarget", hidden);
            SetRectEffectField(effect, "showEase", Ease.Linear);
            SetRectEffectField(effect, "hideEase", Ease.Linear);
            ui.Show();
            return effect;
        }

        private static void AssertRotationAngle(RectEffectController effect, float expected)
        {
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 0f, expected), effect.transform.localRotation),
                Is.LessThan(0.05f), "Rotation must follow the configured unwrapped angular path.");
        }

        [UnityTest]
        public IEnumerator RectRotation_NegativeOvershootReverse_ShouldKeepSignedAngle() =>
            VerifyRotationOvershootContinuation(0f, 90f, Ease.InBack, false);

        [UnityTest]
        public IEnumerator RectRotation_PositiveOvershootReverse_ShouldKeepAngleBeyondOneTurn() =>
            VerifyRotationOvershootContinuation(350f, 440f, Ease.OutBack, false);

        [UnityTest]
        public IEnumerator RectRotation_CanceledNegativeOvershootRetry_ShouldKeepSignedAngle() =>
            VerifyRotationOvershootContinuation(0f, 90f, Ease.InBack, true);

        [UnityTest]
        public IEnumerator RectRotation_CanceledPositiveOvershootRetry_ShouldKeepAngleBeyondOneTurn() =>
            VerifyRotationOvershootContinuation(350f, 440f, Ease.OutBack, true);

        private IEnumerator VerifyRotationOvershootContinuation(
            float shownAngle, float hiddenAngle, Ease curve, bool retryHide)
        {
            foreach (var useGlobal in new[] { false, true })
            {
                var effect = CreateRotationEffect(shownAngle, hiddenAngle, out var ui);
                SetRectEffectField(effect, "hideEase", useGlobal ? curve : Ease.NotSet);
                SetRectEffectField(effect, "hideAngleEase", curve);
                using var initialCancellation = new CancellationTokenSource();
                var initial = CaptureException(ui.HideAsync(initialCancellation.Token));
                var progress = curve == Ease.InBack ? 0.25f : 0.7f;
                SampleRectProgress(effect, progress);
                var overshotAngle = Mathf.LerpUnclamped(shownAngle, hiddenAngle, UIEasing.Evaluate(curve, progress));
                AssertRotationAngle(effect, overshotAngle);
                if (retryHide)
                {
                    initialCancellation.Cancel();
                    yield return WaitForTask(initial);
                    Assert.That(initial.Result, Is.InstanceOf<OperationCanceledException>());
                    SetRectEffectField(effect, "hideEase", Ease.Linear);
                }
                using var continuationCancellation = new CancellationTokenSource();
                var continuation = CaptureException(retryHide
                    ? ui.HideAsync(continuationCancellation.Token) : ui.ShowAsync(continuationCancellation.Token));
                try
                {
                    AssertRotationAngle(effect, overshotAngle); // First frame must preserve the actual orientation.
                    yield return WaitForTask(initial);
                    Assert.That(initial.Result, Is.InstanceOf<OperationCanceledException>());
                    SampleRectProgress(effect, 0.5f);
                    AssertRotationAngle(effect, Mathf.LerpUnclamped(
                        overshotAngle, retryHide ? hiddenAngle : shownAngle, 0.5f));
                    SampleRectProgress(effect, 1f);
                    AssertRotationAngle(effect, retryHide ? hiddenAngle : shownAngle);
                }
                finally
                {
                    initialCancellation.Cancel();
                    continuationCancellation.Cancel();
                }
                yield return WaitForTask(continuation);
                Assert.That(continuation.Result, Is.InstanceOf<OperationCanceledException>());
            }
        }

        [UnityTest]
        public IEnumerator RectRotation_MultiTurnMidflightReverse_ShouldKeepConfiguredWinding()
        {
            var effect = CreateRotationEffect(0f, 720f, out var ui);
            var hiding = CaptureException(ui.HideAsync(CancellationToken.None));
            SampleRectProgress(effect, 0.75f); // 540 degrees is read back by Unity as 180 degrees.
            AssertRotationAngle(effect, 540f);
            using var cancellation = new CancellationTokenSource();
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            try
            {
                yield return WaitForTask(hiding);
                Assert.That(hiding.Result, Is.InstanceOf<OperationCanceledException>());
                SampleRectProgress(effect, 0.5f);
                AssertRotationAngle(effect, 270f); // Retain 540 -> 0, not normalized 180 -> 0.
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(showing);
        }

        [UnityTest]
        public IEnumerator RectRotation_MultiTurnStableEndpoint_ShouldKeepConfiguredWinding()
        {
            var effect = CreateRotationEffect(0f, 720f, out var ui);
            ui.Hide();
            using var cancellation = new CancellationTokenSource();
            var showing = CaptureException(ui.ShowAsync(cancellation.Token));
            try
            {
                SampleRectProgress(effect, 0.25f);
                AssertRotationAngle(effect, 540f); // Retain two turns even though both endpoints look identical.
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(showing);
        }

        [UnityTest]
        public IEnumerator RectRotation_ExternalRotationChange_ShouldUseActualPose()
        {
            var effect = CreateRotationEffect(0f, 90f, out var ui);
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            using var cancellation = new CancellationTokenSource();
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            try
            {
                AssertRotationAngle(effect, 70f);
                SampleRectProgress(effect, 0.5f);
                AssertRotationAngle(effect, 80f);
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(hiding);
        }

        [UnityTest]
        public IEnumerator RectRotation_OrdinaryTargets_ShouldNotSilentlyBecomeShortestPath()
        {
            var effect = CreateRotationEffect(350f, 10f, out var ui);
            using var cancellation = new CancellationTokenSource();
            var hiding = CaptureException(ui.HideAsync(cancellation.Token));
            try
            {
                SampleRectProgress(effect, 0.5f);
                AssertRotationAngle(effect, 180f); // The authored 350 -> 10 path stays a negative 340-degree rotation.
            }
            finally { cancellation.Cancel(); }
            yield return WaitForTask(hiding);
        }
    }
}
