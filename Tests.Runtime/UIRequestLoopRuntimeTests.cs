using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JH.Test_UIManager
{
    public partial class Test_UIBase
    {
        private sealed class LoopLimitScope : IDisposable
        {
            private readonly int _previousLimit = UIOperationSettings.MaxExecutionsPerBehavior;

            internal LoopLimitScope(int limit) => UIOperationSettings.MaxExecutionsPerBehavior = limit;
            public void Dispose() => UIOperationSettings.MaxExecutionsPerBehavior = _previousLimit;
        }

        [Test]
        public void Loop_MutuallyExclusivePanels_ShouldStopBeforeEleventhShowAndKeepDispatcherUsable()
        {
            using var limit = new LoopLimitScope(10);
            var container = CreateObject("MutualLoopRoot");
            var first = CreateUI("LoopA", container.transform);
            var second = CreateUI("LoopB", container.transform);
            var firstShows = 0;
            var secondShows = 0;
            first.ShowActions += () => firstShows++;
            second.ShowActions += () => secondShows++;
            first.AfterUIShowedEvent += second.Show;
            second.AfterUIShowedEvent += first.Show;
            LogAssert.Expect(LogType.Exception, new Regex("UIRequestLoopException: UI循环调度次数超限"));

            first.Show();

            Assert.AreEqual(10, firstShows);
            Assert.AreEqual(10, secondShows);
            Assert.AreEqual(UIState.Hided, first.State);
            Assert.AreEqual(UIState.Showed, second.State, "拒绝第11次请求前不得先隐藏当前互斥面板。");
            var independent = CreateUI("AfterLoop", CreateObject("AfterLoopRoot").transform);
            independent.Show();
            Assert.AreEqual(UIState.Showed, independent.State);
        }

        [UnityTest]
        public IEnumerator Loop_MixedSyncAndAsync_ShouldShareCounterAndReturnDiagnosticException()
        {
            using var limit = new LoopLimitScope(2);
            var ui = CreateUI("MixedLoop", CreateObject("MixedLoopRoot").transform);
            var attempts = new List<Task<Exception>>();
            var shows = 0;
            ui.ShowActions += () => shows++;
            ui.AfterUIShowedEvent += ui.Hide;
            ui.AfterUIHidedEvent += () => attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));

            ui.Show();
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));

            Assert.AreEqual(2, shows);
            Assert.AreEqual(2, attempts.Count);
            Assert.IsNull(attempts[0].Result);
            Assert.That(attempts[1].Result, Is.TypeOf<UIRequestLoopException>());
            var failure = (UIRequestLoopException)attempts[1].Result;
            Assert.AreSame(ui, failure.Panel);
            Assert.IsTrue(failure.IsShow);
            Assert.AreEqual(2, failure.ExecutionLimit);
            StringAssert.Contains("MixedLoopRoot/MixedLoop", failure.PanelPath);
            StringAssert.Contains(".Hide", failure.RequestPath);
            StringAssert.Contains("[本次被阻止]", failure.RequestPath);
            StringAssert.Contains("第 3 次已被阻止", failure.Message);
            StringAssert.Contains(nameof(Loop_MixedSyncAndAsync_ShouldShareCounterAndReturnDiagnosticException),
                failure.RequestOrigin);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [Test]
        public void Loop_FiniteRepetitionBelowLimit_ShouldCompleteNormally()
        {
            using var limit = new LoopLimitScope(10);
            var ui = CreateUI("FiniteLoop", CreateObject("FiniteLoopRoot").transform);
            var shows = 0;
            ui.AfterUIShowedEvent += () =>
            {
                shows++;
                ui.Hide();
            };
            ui.AfterUIHidedEvent += () =>
            {
                if (shows < 3) ui.Show();
            };

            ui.Show();

            Assert.AreEqual(3, shows);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [Test]
        public void Loop_IndependentRequests_ShouldStartFreshChains()
        {
            using var limit = new LoopLimitScope(1);
            var ui = CreateUI("IndependentLoop", CreateObject("IndependentLoopRoot").transform);
            var shows = 0;
            ui.ShowActions += () => shows++;

            for (var index = 0; index < 20; index++)
            {
                ui.Show();
                ui.Hide();
            }

            Assert.AreEqual(20, shows);
        }

        [Test]
        public void Loop_ParallelBranches_ShouldNotAccumulateEachOthersExecutions()
        {
            using var limit = new LoopLimitScope(1);
            var source = CreateUI("BranchSource", CreateObject("BranchSourceRoot").transform);
            var target = CreateUI("BranchTarget", CreateObject("BranchTargetRoot").transform);
            var shows = 0;
            target.ShowActions += () => shows++;
            source.AfterUIShowedEvent += () =>
            {
                target.Show();
                target.Hide();
                target.Show();
            };

            source.Show();

            Assert.AreEqual(2, shows, "并列请求共享父请求，但不互为祖先。");
            Assert.AreEqual(UIState.Showed, target.State);
        }

        [UnityTest]
        public IEnumerator Loop_AlreadyShowedRepeatedRequest_ShouldNotCountAsExecution()
        {
            using var limit = new LoopLimitScope(1);
            var ui = CreateUI("NoOpLoop", CreateObject("NoOpLoopRoot").transform);
            Task<Exception> repeated = null;
            var shows = 0;
            ui.ShowActions += () => shows++;
            ui.AfterUIShowedEvent += () =>
            {
                ui.Show();
                repeated = CaptureException(ui.ShowAsync(CancellationToken.None));
            };

            ui.Show();
            yield return WaitForTask(repeated);

            Assert.IsNull(repeated.Result);
            Assert.AreEqual(1, shows);
        }

        [Test]
        public void Loop_AdmissionRejectedRepeatedRequest_ShouldNotTriggerGuard()
        {
            using var limit = new LoopLimitScope(1);
            var container = CreateObject("RejectedLoopRoot");
            var low = CreateUI("RejectedLoopLow", container.transform);
            var high = CreateUI("RejectedLoopHigh", container.transform);
            high.MutexPriority = 1;
            var lowShows = 0;
            low.ShowActions += () => lowShows++;
            low.AfterUIShowedEvent += high.Show;
            high.AfterUIShowedEvent += low.Show;

            low.Show();

            Assert.AreEqual(1, lowShows);
            Assert.AreEqual(UIState.Hided, low.State);
            Assert.AreEqual(UIState.Showed, high.State);
        }

        [Test]
        public void Loop_SameNameDifferentInstances_ShouldNotMatch()
        {
            using var limit = new LoopLimitScope(1);
            var first = CreateUI("SameName", CreateObject("FirstInstanceRoot").transform);
            var second = CreateUI("SameName", CreateObject("SecondInstanceRoot").transform);
            first.AfterUIShowedEvent += second.Show;

            first.Show();

            Assert.AreEqual(UIState.Showed, first.State);
            Assert.AreEqual(UIState.Showed, second.State);
        }

        [UnityTest]
        public IEnumerator Loop_AcrossAnimationFrames_ShouldRetainAncestryWhenQueueWasEmpty()
        {
            using var limit = new LoopLimitScope(2);
            var ui = CreateUI("AsyncFrameLoop", CreateObject("AsyncFrameLoopRoot").transform);
            var attempts = new List<Task<Exception>>();
            var shows = 0;
            ui.ShowAsyncActions += _ =>
            {
                shows++;
                return UniTask.NextFrame();
            };
            ui.AfterUIShowedEvent += ui.Hide;
            ui.AfterUIHidedEvent += () => attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));
            attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(1, shows);

            var deadline = Time.realtimeSinceStartup + TestTimeout;
            while (attempts.Count < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(3, attempts.Count, "跨帧后仍须在第3次显示前触发保护。");
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));

            Assert.IsNull(attempts[0].Result);
            Assert.IsNull(attempts[1].Result);
            Assert.That(attempts[2].Result, Is.TypeOf<UIRequestLoopException>());
            Assert.AreEqual(2, shows);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [UnityTest]
        public IEnumerator Loop_HideIntent_ShouldHaveItsOwnCounter()
        {
            using var limit = new LoopLimitScope(1);
            var ui = CreateUI("HideLoop", CreateObject("HideLoopRoot").transform);
            ui.Show();
            var hides = 0;
            ui.HideAsyncActions += _ =>
            {
                hides++;
                return UniTask.NextFrame();
            };
            Task<Exception> repeated = null;
            ui.AfterUIHidedEvent += ui.Show;
            ui.AfterUIShowedEvent += () => repeated = CaptureException(ui.HideAsync(CancellationToken.None));
            var original = CaptureException(ui.HideAsync(CancellationToken.None));

            var deadline = Time.realtimeSinceStartup + TestTimeout;
            while (repeated == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(repeated);
            yield return WaitForTask(Task.WhenAll(original, repeated));

            Assert.IsNull(original.Result);
            Assert.That(repeated.Result, Is.TypeOf<UIRequestLoopException>());
            Assert.IsFalse(((UIRequestLoopException)repeated.Result).IsShow);
            Assert.AreEqual(1, hides);
            Assert.AreEqual(UIState.Showed, ui.State);
        }

        [UnityTest]
        public IEnumerator Loop_BlockedShow_ShouldNotCancelAnExistingHideOrLoseItsCompletion()
        {
            using var limit = new LoopLimitScope(1);
            var ui = CreateUI("PendingHideLoop", CreateObject("PendingHideLoopRoot").transform);
            var hideAnimation = new UniTaskCompletionSource();
            var hideToken = default(CancellationToken);
            Task<Exception> hide = null;
            Task<Exception> blockedShow = null;
            ui.HideAsyncActions += token =>
            {
                hideToken = token;
                return hideAnimation.Task;
            };
            ui.AfterUIShowedEvent += () => hide = CaptureException(ui.HideAsync(CancellationToken.None));
            ui.BeforeUIHideEvent += () => blockedShow = CaptureException(ui.ShowAsync(CancellationToken.None));

            ui.Show();
            Assert.IsNotNull(blockedShow);
            Assert.IsTrue(blockedShow.IsCompleted, "被拒绝请求的任务必须完成，不能留在等待中。");
            Assert.That(blockedShow.Result, Is.TypeOf<UIRequestLoopException>());
            Assert.IsFalse(hideToken.IsCancellationRequested, "超限检查必须早于取消现有操作。");
            Assert.AreEqual(UIState.Hiding, ui.State);
            hideAnimation.TrySetResult();
            yield return WaitForTask(hide);

            Assert.IsNull(hide.Result);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [UnityTest]
        public IEnumerator Loop_ConfigurationChange_ShouldOnlyAffectNewRootChains()
        {
            using var limit = new LoopLimitScope(2);
            var ui = CreateUI("SnapshotLoop", CreateObject("SnapshotLoopRoot").transform);
            var attempts = new List<Task<Exception>>();
            ui.AfterUIShowedEvent += () =>
            {
                UIOperationSettings.MaxExecutionsPerBehavior = 1;
                ui.Hide();
            };
            ui.AfterUIHidedEvent += () => attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));

            ui.Show();
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));
            Assert.AreEqual(2, attempts.Count);
            Assert.IsNull(attempts[0].Result);
            Assert.AreEqual(2, ((UIRequestLoopException)attempts[1].Result).ExecutionLimit);

            attempts.Clear();
            ui.Show();
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));
            Assert.AreEqual(1, attempts.Count);
            Assert.AreEqual(1, ((UIRequestLoopException)attempts[0].Result).ExecutionLimit);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Loop_InvalidLimit_ShouldRejectWithoutChangingConfiguration(int value)
        {
            var original = UIOperationSettings.MaxExecutionsPerBehavior;
            Assert.Throws<ArgumentOutOfRangeException>(() => UIOperationSettings.MaxExecutionsPerBehavior = value);
            Assert.AreEqual(original, UIOperationSettings.MaxExecutionsPerBehavior);
        }
    }
}
