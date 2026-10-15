using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JH.Test_UIManager
{
    /// <summary>公开请求串行派发、内部关系联动和异步恢复的回归验证。</summary>
    public partial class Test_UIBase
    {
        [Test]
        public void Show_HideFromShowing_ShouldFinishNotificationAndDrainBeforeReturning()
        {
            var ui = CreateUI("QueuedHide", CreateObject("QueuedHideRoot").transform);
            var events = new List<string>();
            var frame = Time.frameCount;
            ui.OnStateChanged += (_, state) =>
            {
                events.Add($"A:{state}");
                if (state != UIState.Showing) return;
                ui.Hide();
                Assert.AreEqual(UIState.Showing, ui.State, "回调中的新请求不能立即改写当前状态。");
            };
            ui.OnStateChanged += (_, state) =>
            {
                events.Add($"B:{state}");
                Assert.AreEqual(state, ui.State, "后续订阅者应观察到本轮通知对应的状态。");
            };

            ui.Show();

            CollectionAssert.AreEqual(new[]
            {
                "A:Showing", "B:Showing", "A:Showed", "B:Showed",
                "A:Hiding", "B:Hiding", "A:Hided", "B:Hided"
            }, events);
            Assert.AreEqual(UIState.Hided, ui.State);
            Assert.AreEqual(frame, Time.frameCount, "消费队列不应等待下一帧。");
        }

        [TestCase("BeforeShow", UIState.Showing)]
        [TestCase("AfterShow", UIState.Showed)]
        [TestCase("BeforeHide", UIState.Hiding)]
        [TestCase("AfterHide", UIState.Hided)]
        public void Lifecycle_ReentrantRequest_ShouldNotInterruptRemainingSubscribers(string notification,
            UIState expectedState)
        {
            var ui = CreateUI(notification, CreateObject(notification + "Root").transform);
            var showing = notification.EndsWith("Show");
            if (!showing) ui.Show();
            var observed = false;
            Action first = () =>
            {
                if (showing) ui.Hide();
                else ui.Show();
                Assert.AreEqual(expectedState, ui.State);
            };
            Action second = () =>
            {
                observed = true;
                Assert.AreEqual(expectedState, ui.State);
            };
            switch (notification)
            {
                case "BeforeShow": ui.BeforeUIShowEvent += first; ui.BeforeUIShowEvent += second; break;
                case "AfterShow": ui.AfterUIShowedEvent += first; ui.AfterUIShowedEvent += second; break;
                case "BeforeHide": ui.BeforeUIHideEvent += first; ui.BeforeUIHideEvent += second; break;
                case "AfterHide": ui.AfterUIHidedEvent += first; ui.AfterUIHidedEvent += second; break;
            }

            if (showing) ui.Show();
            else ui.Hide();

            Assert.IsTrue(observed);
            Assert.AreEqual(showing ? UIState.Hided : UIState.Showed, ui.State);
        }

        [Test]
        public void Show_SeveralQueuedRequests_ShouldCheckStateAtExecutionAndPreserveOrder()
        {
            var source = CreateUI("QueueSource", CreateObject("QueueSourceRoot").transform);
            var target = CreateUI("QueueTarget", CreateObject("QueueTargetRoot").transform);
            var states = new List<UIState>();
            target.OnStateChanged += (_, state) => states.Add(state);
            source.BeforeUIShowEvent += () =>
            {
                target.Show();
                target.Hide();
                target.Show();
                Assert.AreEqual(UIState.Hided, target.State);
            };

            source.Show();

            CollectionAssert.AreEqual(new[]
            {
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided,
                UIState.Showing, UIState.Showed
            }, states);
        }

        [Test]
        public void Show_ChildRequestsParentHide_ShouldFinishChildNotificationBeforeClosingSubtree()
        {
            var parent = CreateUI("QueuedParent", CreateObject("QueuedParentRoot").transform);
            var child = CreateUI("QueuedChild", parent.transform);
            child.OnStateChanged += (_, state) =>
            {
                if (state == UIState.Showing) parent.Hide();
            };
            child.OnStateChanged += (_, state) => Assert.AreEqual(state, child.State);

            child.Show();

            Assert.AreEqual(UIState.Hided, parent.State);
            Assert.AreEqual(UIState.Hided, child.State);
        }

        [UnityTest]
        public IEnumerator ShowAsync_PendingAnimation_ShouldNotBlockLaterHideOrCommitStaleShowed()
        {
            var ui = CreateUI("InterruptQueuedAnimation", CreateObject("InterruptQueuedAnimationRoot").transform);
            var animation = new UniTaskCompletionSource();
            ui.ShowAsyncActions += _ => animation.Task;
            var afterShow = 0;
            ui.AfterUIShowedEvent += () => afterShow++;
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
            Assert.AreEqual(UIState.Showing, ui.State);

            ui.Hide();
            Assert.AreEqual(UIState.Hided, ui.State, "挂起动画不能占住消费器。");
            Assert.IsFalse(operation.IsCompleted, "忽略取消的已启动行为仍需被观察。");
            animation.TrySetResult();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(0, afterShow);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [UnityTest]
        public IEnumerator ShowAsync_CompletionNotification_ShouldAlsoDeferNewRequests()
        {
            var ui = CreateUI("CompletionNotification", CreateObject("CompletionNotificationRoot").transform);
            var animation = new UniTaskCompletionSource();
            ui.ShowAsyncActions += _ => animation.Task;
            var observerCalled = false;
            ui.OnStateChanged += (_, state) =>
            {
                if (state == UIState.Showed) ui.Hide();
            };
            ui.OnStateChanged += (_, state) =>
            {
                if (state != UIState.Showed) return;
                observerCalled = true;
                Assert.AreEqual(UIState.Showed, ui.State);
            };
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));

            // 从消费器之外完成动画，验证恢复生命周期会重新进入调度器。
            animation.TrySetResult();
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result, "显示完成通知提出的 Hide 应在本段显示提交之后执行。");
            Assert.IsTrue(observerCalled);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [UnityTest]
        public IEnumerator HideAsync_CompletionNotification_ShouldAlsoDeferNewRequests()
        {
            var ui = CreateUI("HideCompletion", CreateObject("HideCompletionRoot").transform);
            ui.Show();
            var animation = new UniTaskCompletionSource();
            ui.HideAsyncActions += _ => animation.Task;
            var observerCalled = false;
            ui.AfterUIHidedEvent += ui.Show;
            ui.AfterUIHidedEvent += () =>
            {
                observerCalled = true;
                Assert.AreEqual(UIState.Hided, ui.State);
            };
            var operation = CaptureException(ui.HideAsync(CancellationToken.None));

            animation.TrySetResult();
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result);
            Assert.IsTrue(observerCalled);
            Assert.AreEqual(UIState.Showed, ui.State);
        }

        [UnityTest]
        public IEnumerator QueuedAsyncRequest_CanceledBeforeExecution_ShouldHaveNoSideEffects()
        {
            var source = CreateUI("CancelQueueSource", CreateObject("CancelQueueSourceRoot").transform);
            var target = CreateUI("CancelQueueTarget", CreateObject("CancelQueueTargetRoot").transform);
            using var cancellation = new CancellationTokenSource();
            Task<Exception> operation = null;
            var notifications = 0;
            target.OnStateChanged += (_, _) => notifications++;
            source.BeforeUIShowEvent += () =>
            {
                operation = CaptureException(target.ShowAsync(cancellation.Token));
                Assert.IsFalse(operation.IsCompleted);
                cancellation.Cancel();
            };

            source.Show();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(UIState.Hided, target.State);
        }

        [UnityTest]
        public IEnumerator QueuedAsyncRequest_DestroyedBeforeExecution_ShouldCancelAndContinueQueue()
        {
            var source = CreateUI("DestroyQueueSource", CreateObject("DestroyQueueSourceRoot").transform);
            var target = CreateUI("DestroyQueueTarget", CreateObject("DestroyQueueTargetRoot").transform);
            var next = CreateUI("AfterDestroyedTarget", CreateObject("AfterDestroyedRoot").transform);
            Task<Exception> operation = null;
            source.BeforeUIShowEvent += () =>
            {
                operation = CaptureException(target.ShowAsync(CancellationToken.None));
                target.Hide();
                Object.DestroyImmediate(target.gameObject);
                next.Show();
            };

            source.Show();
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(UIState.Showed, next.State);
        }

        [Test]
        public void Show_CallbackThrows_ShouldPropagateAndStillConsumeQueuedRequests()
        {
            var source = CreateUI("FaultQueueSource", CreateObject("FaultQueueSourceRoot").transform);
            var next = CreateUI("FaultQueueNext", CreateObject("FaultQueueNextRoot").transform);
            var failure = new InvalidOperationException("request callback failed");
            source.BeforeUIShowEvent += () =>
            {
                next.Show();
                throw failure;
            };

            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(source.Show));
            Assert.AreEqual(UIState.Showed, next.State);
            next.Hide();
            Assert.AreEqual(UIState.Hided, next.State, "异常不能让消费标记永久保持忙碌。");
        }

        [Test]
        public void DeferredSyncRequest_Throws_ShouldLogAndContinueOtherRequests()
        {
            var source = CreateUI("DeferredFaultSource", CreateObject("DeferredFaultSourceRoot").transform);
            var broken = CreateUI("DeferredFaultTarget", CreateObject("DeferredFaultTargetRoot").transform);
            var next = CreateUI("DeferredFaultNext", CreateObject("DeferredFaultNextRoot").transform);
            broken.BeforeUIShowEvent += () => throw new InvalidOperationException("deferred request failed");
            source.BeforeUIShowEvent += () =>
            {
                broken.Show();
                next.Show();
            };
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: deferred request failed");

            source.Show();

            Assert.AreEqual(UIState.Showed, next.State);
        }

        [Test]
        public void Show_LongFiniteRequestChain_ShouldDrainWithoutRecursiveDispatch()
        {
            var ui = CreateUI("FiniteChain", CreateObject("FiniteChainRoot").transform);
            var transitions = 0;
            ui.OnStateChanged += (_, state) =>
            {
                if (state != UIState.Showed && state != UIState.Hided) return;
                transitions++;
                if (transitions >= 1024) return;
                if (state == UIState.Showed) ui.Hide();
                else ui.Show();
            };

            ui.Show();

            Assert.AreEqual(1024, transitions);
            Assert.AreEqual(UIState.Hided, ui.State);
        }
    }
}
