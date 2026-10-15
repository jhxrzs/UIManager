using System;
using System.Collections;
using System.Collections.Generic;
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
        [TestCase("OnStateChanged", UIState.Showing, true)]
        [TestCase("OnStateChanged", UIState.Showed, true)]
        [TestCase("OnStateChanged", UIState.Hiding, false)]
        [TestCase("OnStateChanged", UIState.Hided, false)]
        [TestCase("BeforeUIShowEvent", UIState.Showing, true)]
        [TestCase("AfterUIShowedEvent", UIState.Showed, true)]
        [TestCase("BeforeUIHideEvent", UIState.Hiding, false)]
        [TestCase("AfterUIHidedEvent", UIState.Hided, false)]
        public void Notification_MultipleSubscriberFailures_ShouldContinueInOrderAndCompleteLifecycle(
            string eventName, UIState eventState, bool showing)
        {
            var ui = CreateUI("Notify" + eventName, CreateObject("NotifyRoot" + eventState).transform);
            if (!showing) ui.Show();
            var order = new List<string>();
            var observedStates = new List<UIState>();
            var reports = new List<string>();
            var actions = 0;
            ui.ShowActions += () => actions++;
            ui.HideActions += () => actions++;
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                order.Add("A");
                throw new InvalidOperationException("first subscriber failed");
            });
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                order.Add("B");
                observedStates.Add(ui.State);
            });
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                order.Add("C");
                throw new ArgumentException("third subscriber failed");
            });
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                order.Add("D");
                observedStates.Add(ui.State);
            });
            ExpectNotificationError("InvalidOperationException: first subscriber failed");
            ExpectNotificationError("ArgumentException: third subscriber failed");
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Exception) reports.Add(message + "\n" + stack);
            };
            Application.logMessageReceived += capture;
            try
            {
                Assert.DoesNotThrow(() =>
                {
                    if (showing) ui.Show();
                    else ui.Hide();
                });
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, order);
            CollectionAssert.AreEqual(new[] { eventState, eventState }, observedStates);
            Assert.AreEqual(1, actions);
            Assert.AreEqual(showing ? UIState.Showed : UIState.Hided, ui.State);
            Assert.AreEqual(2, reports.Count);
            StringAssert.Contains(ui.name, reports[0]);
            StringAssert.Contains(eventName, reports[0]);
            StringAssert.Contains("订阅方法", reports[0]);
            StringAssert.Contains("UINotificationException", reports[0]);
            StringAssert.Contains("first subscriber failed", reports[0]);
            StringAssert.Contains("third subscriber failed", reports[1]);
            StringAssert.Contains(nameof(Notification_MultipleSubscriberFailures_ShouldContinueInOrderAndCompleteLifecycle),
                reports[0]);
        }

        [UnityTest]
        public IEnumerator Notification_AsyncLifecycle_ShouldIsolateErrorsBeforeAndAfterAwait()
        {
            var eventNames = new[]
            {
                "OnStateChanged", "OnStateChanged", "OnStateChanged", "OnStateChanged",
                "BeforeUIShowEvent", "AfterUIShowedEvent", "BeforeUIHideEvent", "AfterUIHidedEvent"
            };
            var eventStates = new[]
            {
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided,
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided
            };
            for (var index = 0; index < eventNames.Length; index++)
            {
                var eventName = eventNames[index];
                var eventState = eventStates[index];
                var showing = eventState == UIState.Showing || eventState == UIState.Showed;
                var ui = CreateUI("AsyncNotify" + index, CreateObject("AsyncNotifyRoot" + index).transform);
                if (!showing) ui.Show();
                ui.ShowAsyncActions += _ => UniTask.NextFrame();
                ui.HideAsyncActions += _ => UniTask.NextFrame();
                var observerCalls = 0;
                var observedState = UIState.Hided;
                SubscribeNotification(ui, eventName, eventState,
                    () => throw new InvalidOperationException("async lifecycle notification failed"));
                SubscribeNotification(ui, eventName, eventState, () =>
                {
                    observerCalls++;
                    observedState = ui.State;
                });
                ExpectNotificationError("InvalidOperationException: async lifecycle notification failed");

                var operation = CaptureException(showing
                    ? ui.ShowAsync(CancellationToken.None)
                    : ui.HideAsync(CancellationToken.None));
                yield return WaitForTask(operation);

                Assert.IsNull(operation.Result, eventName + " 的通知异常不应使异步显隐失败。");
                Assert.AreEqual(1, observerCalls);
                Assert.AreEqual(eventState, observedState);
                Assert.AreEqual(showing ? UIState.Showed : UIState.Hided, ui.State);
            }
        }

        [Test]
        public void Notification_FailureAfterQueuingRequest_ShouldFinishSubscribersAndConsumeRequest()
        {
            var ui = CreateUI("QueuedNotify", CreateObject("QueuedNotifyRoot").transform);
            var order = new List<string>();
            ui.BeforeUIShowEvent += () =>
            {
                order.Add("A");
                ui.Hide();
                throw new InvalidOperationException("queued notification failed");
            };
            ui.BeforeUIShowEvent += () => order.Add("B:" + ui.State);
            ui.ShowActions += () => order.Add("Show");
            ui.HideActions += () => order.Add("Hide");
            ExpectNotificationError("InvalidOperationException: queued notification failed");

            ui.Show();

            CollectionAssert.AreEqual(new[] { "A", "B:Showing", "Show", "Hide" }, order);
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        [Test]
        public void Notification_SubscriptionChangesAndFailure_ShouldKeepCurrentSnapshot()
        {
            var ui = CreateUI("SnapshotNotify", CreateObject("SnapshotNotifyRoot").transform);
            var order = new List<string>();
            var firstTime = true;
            Action second = () => order.Add("B");
            Action added = () => order.Add("C");
            ui.BeforeUIShowEvent += () =>
            {
                order.Add("A");
                if (!firstTime) return;
                firstTime = false;
                ui.BeforeUIShowEvent -= second;
                ui.BeforeUIShowEvent += added;
                throw new InvalidOperationException("snapshot notification failed");
            };
            ui.BeforeUIShowEvent += second;
            ExpectNotificationError("InvalidOperationException: snapshot notification failed");

            ui.Show();
            ui.Hide();
            ui.Show();

            CollectionAssert.AreEqual(new[] { "A", "B", "A", "C" }, order);
        }

        [UnityTest]
        public IEnumerator Notification_ThrownCancellationException_ShouldNotCancelLifecycle()
        {
            var ui = CreateUI("CanceledNotify", CreateObject("CanceledNotifyRoot").transform);
            ui.BeforeUIShowEvent += () => throw new OperationCanceledException("subscriber-local cancellation");
            ExpectNotificationError("OperationCanceledException: subscriber-local cancellation");

            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
            yield return WaitForTask(operation);

            Assert.IsNull(operation.Result);
            Assert.AreEqual(UIState.Showed, ui.State);
        }

        [UnityTest]
        public IEnumerator Notification_ExplicitTokenCancellation_ShouldStillCancelLifecycle()
        {
            var ui = CreateUI("ExplicitCancelNotify", CreateObject("ExplicitCancelNotifyRoot").transform);
            using var cancellation = new CancellationTokenSource();
            var observerCalls = 0;
            var actionCalls = 0;
            ui.BeforeUIShowEvent += () =>
            {
                cancellation.Cancel();
                throw new InvalidOperationException("notification failed after explicit cancellation");
            };
            ui.BeforeUIShowEvent += () => observerCalls++;
            ui.ShowAsyncActions += _ =>
            {
                actionCalls++;
                return UniTask.CompletedTask;
            };
            ExpectNotificationError("InvalidOperationException: notification failed after explicit cancellation");

            var operation = CaptureException(ui.ShowAsync(cancellation.Token));
            yield return WaitForTask(operation);

            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            Assert.AreEqual(1, observerCalls);
            Assert.AreEqual(0, actionCalls);
            Assert.AreNotEqual(UIState.Showed, ui.State);
        }

        [UnityTest]
        public IEnumerator Notification_ActionFailures_ShouldStillFailTheirOwnAsyncOperations()
        {
            var show = CreateUI("FailedShowAction", CreateObject("FailedShowActionRoot").transform);
            var hide = CreateUI("FailedHideAction", CreateObject("FailedHideActionRoot").transform);
            hide.Show();
            var showFailure = new InvalidOperationException("show action failed");
            var hideFailure = new InvalidOperationException("hide action failed");
            var afterShow = 0;
            var afterHide = 0;
            show.BeforeUIShowEvent += () => throw new ArgumentException("show notification failed");
            hide.BeforeUIHideEvent += () => throw new ArgumentException("hide notification failed");
            show.ShowAsyncActions += _ => UniTask.FromException(showFailure);
            hide.HideAsyncActions += _ => UniTask.FromException(hideFailure);
            show.AfterUIShowedEvent += () => afterShow++;
            hide.AfterUIHidedEvent += () => afterHide++;
            ExpectNotificationError("ArgumentException: show notification failed");
            ExpectNotificationError("ArgumentException: hide notification failed");

            var showOperation = CaptureException(show.ShowAsync(CancellationToken.None));
            var hideOperation = CaptureException(hide.HideAsync(CancellationToken.None));
            yield return WaitForTask(Task.WhenAll(showOperation, hideOperation));

            Assert.AreSame(showFailure, showOperation.Result);
            Assert.AreSame(hideFailure, hideOperation.Result);
            Assert.AreEqual(0, afterShow);
            Assert.AreEqual(0, afterHide);
        }

        private static void SubscribeNotification(UIBase ui, string eventName, UIState eventState, Action callback)
        {
            switch (eventName)
            {
                case "OnStateChanged":
                    ui.OnStateChanged += (_, state) =>
                    {
                        if (state == eventState) callback();
                    };
                    break;
                case "BeforeUIShowEvent": ui.BeforeUIShowEvent += callback; break;
                case "AfterUIShowedEvent": ui.AfterUIShowedEvent += callback; break;
                case "BeforeUIHideEvent": ui.BeforeUIHideEvent += callback; break;
                case "AfterUIHidedEvent": ui.AfterUIHidedEvent += callback; break;
                default: throw new ArgumentOutOfRangeException(nameof(eventName));
            }
        }

        private static void ExpectNotificationError(string exceptionMessage)
        {
            // Unity 将 InnerException 作为日志首行，包装异常的上下文位于展开后的异常栈中。
            LogAssert.Expect(LogType.Exception, exceptionMessage);
        }
    }
}
