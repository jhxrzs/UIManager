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
    /// <summary>验证 UIBase 的通知异常隔离与订阅快照。</summary>
    public partial class Test_UIBase
    {
        /// <summary>验证多个通知订阅者抛错后仍按顺序通知其余订阅者并完成生命周期。</summary>
        /// <param name="eventName">要订阅的生命周期事件名称。</param>
        /// <param name="eventState">状态通知需要匹配的目标状态。</param>
        /// <param name="showing">是否验证显示方向；为 false 时验证隐藏方向。</param>
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
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("Notify" + eventName, CreateObject("NotifyRoot" + eventState).transform);
            // 根据“!showing”选择对应测试路径。
            if (!showing) ui.Show();
            // 创建“order”，隔离本用例的资源或观测数据。
            var order = new List<string>();
            // 创建“observedStates”，隔离本用例的资源或观测数据。
            var observedStates = new List<UIState>();
            // 创建“reports”，隔离本用例的资源或观测数据。
            var reports = new List<string>();
            // 初始化“actions”为0，作为后续回调的观测基准。
            var actions = 0;
            // 为“ui.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowActions += () => actions++;
            // 为“ui.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideActions += () => actions++;
            // 订阅会记录顺序并抛错的通知回调，验证异常不会中断后续订阅者。
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                // 将本轮对象或观测值加入“order”，供统一清理或顺序断言使用。
                order.Add("A");
                // 抛出预设异常，验证调用方的失败传播与收尾行为。
                throw new InvalidOperationException("first subscriber failed");
            });
            // 订阅指定生命周期通知，记录订阅者顺序及其观察到的状态。
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                // 将本轮对象或观测值加入“order”，供统一清理或顺序断言使用。
                order.Add("B");
                // 将本轮对象或观测值加入“observedStates”，供统一清理或顺序断言使用。
                observedStates.Add(ui.State);
            });
            // 订阅会记录顺序并抛错的通知回调，验证异常不会中断后续订阅者。
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                // 将本轮对象或观测值加入“order”，供统一清理或顺序断言使用。
                order.Add("C");
                // 抛出预设异常，验证调用方的失败传播与收尾行为。
                throw new ArgumentException("third subscriber failed");
            });
            // 订阅指定生命周期通知，记录订阅者顺序及其观察到的状态。
            SubscribeNotification(ui, eventName, eventState, () =>
            {
                // 将本轮对象或观测值加入“order”，供统一清理或顺序断言使用。
                order.Add("D");
                // 将本轮对象或观测值加入“observedStates”，供统一清理或顺序断言使用。
                observedStates.Add(ui.State);
            });
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("InvalidOperationException: first subscriber failed");
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("ArgumentException: third subscriber failed");
            // 准备收集异常消息与堆栈的日志回调，验证每个失败订阅者都被报告。
            Application.LogCallback capture = (message, stack, type) =>
            {
                // 根据“type == LogType.Exception”选择对应测试路径。
                if (type == LogType.Exception) reports.Add(message + "\n" + stack);
            };
            // 为“Application.logMessageReceived”追加观测或重入回调，验证通知次数及执行顺序。
            Application.logMessageReceived += capture;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 确认当前被测调用安全完成，不产生意外异常。
                Assert.DoesNotThrow(() =>
                {
                    // 根据显示分支或操作（showing）选择对应测试路径。
                    if (showing) ui.Show();
                    // 覆盖条件不成立时的另一条配置或清理路径。
                    else ui.Hide();
                });
            }
            finally
            {
                // 解除“Application.logMessageReceived”的测试订阅，避免干扰后续用例。
                Application.logMessageReceived -= capture;
            }

            // 确认“order”等于“new[] { "A", "B", "C", "D" }”。
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, order);
            // 确认“observedStates”等于“new[] { eventState, eventState }”。
            CollectionAssert.AreEqual(new[] { eventState, eventState }, observedStates);
            // 确认“actions”等于1。
            Assert.AreEqual(1, actions);
            // 确认界面（ui）的界面状态等于“showing ? UIState.Showed : UIState.Hided”。
            Assert.AreEqual(showing ? UIState.Showed : UIState.Hided, ui.State);
            // 确认“reports”的元素数量等于2。
            Assert.AreEqual(2, reports.Count);
            // 确认“reports[0]”包含界面（ui）的对象名称。
            StringAssert.Contains(ui.name, reports[0]);
            // 确认“reports[0]”包含“eventName”。
            StringAssert.Contains(eventName, reports[0]);
            // 确认“reports[0]”包含“"订阅方法"”。
            StringAssert.Contains("订阅方法", reports[0]);
            // 确认“reports[0]”包含“"UINotificationException"”。
            StringAssert.Contains("UINotificationException", reports[0]);
            // 确认“reports[0]”包含“"first subscriber failed"”。
            StringAssert.Contains("first subscriber failed", reports[0]);
            // 确认“reports[1]”包含“"third subscriber failed"”。
            StringAssert.Contains("third subscriber failed", reports[1]);
            // 确认“reports[0]”包含“nameof(Notification_MultipleSubscriberFailures_ShouldContinueInOrderAndCompleteLifecycle)”。
            StringAssert.Contains(nameof(Notification_MultipleSubscriberFailures_ShouldContinueInOrderAndCompleteLifecycle),
                reports[0]);
        }

        /// <summary>验证异步生命周期在等待前后的通知异常均单独隔离。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Notification_AsyncLifecycle_ShouldIsolateErrorsBeforeAndAfterAwait()
        {
            // 创建“eventNames”，隔离本用例的资源或观测数据。
            var eventNames = new[]
            {
                "OnStateChanged", "OnStateChanged", "OnStateChanged", "OnStateChanged",
                "BeforeUIShowEvent", "AfterUIShowedEvent", "BeforeUIHideEvent", "AfterUIHidedEvent"
            };
            // 创建“eventStates”，隔离本用例的资源或观测数据。
            var eventStates = new[]
            {
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided,
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided
            };
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var index = 0; index < eventNames.Length; index++)
            {
                // 保存“eventName”的基准值“eventNames[index]”，供后续比较或恢复。
                var eventName = eventNames[index];
                // 保存“eventState”的基准值“eventStates[index]”，供后续比较或恢复。
                var eventState = eventStates[index];
                // 保存显示分支或操作（showing）的基准值“eventState == UIState.Showing || eventState == UIState.Showed”，供后续比较或恢复。
                var showing = eventState == UIState.Showing || eventState == UIState.Showed;
                // 创建界面（ui）并配置测试所需的层级或表现组件。
                var ui = CreateUI("AsyncNotify" + index, CreateObject("AsyncNotifyRoot" + index).transform);
                // 根据“!showing”选择对应测试路径。
                if (!showing) ui.Show();
                // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                ui.ShowAsyncActions += _ => UniTask.NextFrame();
                // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                ui.HideAsyncActions += _ => UniTask.NextFrame();
                // 初始化“observerCalls”为0，作为后续回调的观测基准。
                var observerCalls = 0;
                // 保存“observedState”的基准值隐藏终态，供后续比较或恢复。
                var observedState = UIState.Hided;
                // 订阅会记录顺序并抛错的通知回调，验证异常不会中断后续订阅者。
                SubscribeNotification(ui, eventName, eventState,
                    () => throw new InvalidOperationException("async lifecycle notification failed"));
                // 订阅指定生命周期通知，记录订阅者顺序及其观察到的状态。
                SubscribeNotification(ui, eventName, eventState, () =>
                {
                    // 更新“observerCalls”的计数，记录回调实际执行次数。
                    observerCalls++;
                    // 将“observedState”设为界面（ui）的界面状态，建立当前测试条件。
                    observedState = ui.State;
                });
                // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
                ExpectNotificationError("InvalidOperationException: async lifecycle notification failed");

                // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
                var operation = CaptureException(showing
                    ? ui.ShowAsync(CancellationToken.None)
                    : ui.HideAsync(CancellationToken.None));
                // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
                yield return WaitForTask(operation);

                // 确认异步操作（operation）的异步结果为空。
                Assert.IsNull(operation.Result, eventName + " 的通知异常不应使异步显隐失败。");
                // 确认“observerCalls”等于1。
                Assert.AreEqual(1, observerCalls);
                // 确认“observedState”等于“eventState”。
                Assert.AreEqual(eventState, observedState);
                // 确认界面（ui）的界面状态等于“showing ? UIState.Showed : UIState.Hided”。
                Assert.AreEqual(showing ? UIState.Showed : UIState.Hided, ui.State);
            }
        }

        /// <summary>验证通知提交请求后抛错仍完成剩余订阅者并消费已排队请求。</summary>
        [Test]
        public void Notification_FailureAfterQueuingRequest_ShouldFinishSubscribersAndConsumeRequest()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("QueuedNotify", CreateObject("QueuedNotifyRoot").transform);
            // 创建“order”，隔离本用例的资源或观测数据。
            var order = new List<string>();
            // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIShowEvent += () =>
            {
                // 将本轮对象或观测值加入“order”，供统一清理或顺序断言使用。
                order.Add("A");
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
                // 抛出预设异常，验证调用方的失败传播与收尾行为。
                throw new InvalidOperationException("queued notification failed");
            };
            // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIShowEvent += () => order.Add("B:" + ui.State);
            // 为“ui.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowActions += () => order.Add("Show");
            // 为“ui.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideActions += () => order.Add("Hide");
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("InvalidOperationException: queued notification failed");

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 确认“order”等于“new[] { "A", "B:Showing", "Show", "Hide" }”。
            CollectionAssert.AreEqual(new[] { "A", "B:Showing", "Show", "Hide" }, order);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证通知中增删订阅者并抛错不改变本次派发快照。</summary>
        [Test]
        public void Notification_SubscriptionChangesAndFailure_ShouldKeepCurrentSnapshot()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("SnapshotNotify", CreateObject("SnapshotNotifyRoot").transform);
            // 创建“order”，隔离本用例的资源或观测数据。
            var order = new List<string>();
            // 保存“firstTime”的基准值true，供后续比较或恢复。
            var firstTime = true;
            // 准备记录 B 的原有订阅者，用于验证派发中解绑不会改变当前快照。
            Action second = () => order.Add("B");
            // 准备记录 C 的新订阅者，用于验证派发中新增监听只影响后续通知。
            Action added = () => order.Add("C");
            // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIShowEvent += () =>
            {
                // 将本轮对象或观测值加入“order”，供统一清理或顺序断言使用。
                order.Add("A");
                // 根据“!firstTime”选择对应测试路径。
                if (!firstTime) return;
                // 将“firstTime”设为false，建立当前测试条件。
                firstTime = false;
                // 解除“ui.BeforeUIShowEvent”的测试订阅，避免干扰后续用例。
                ui.BeforeUIShowEvent -= second;
                // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
                ui.BeforeUIShowEvent += added;
                // 抛出预设异常，验证调用方的失败传播与收尾行为。
                throw new InvalidOperationException("snapshot notification failed");
            };
            // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIShowEvent += second;
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("InvalidOperationException: snapshot notification failed");

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 确认“order”等于“new[] { "A", "B", "A", "C" }”。
            CollectionAssert.AreEqual(new[] { "A", "B", "A", "C" }, order);
        }

        /// <summary>验证通知订阅者主动抛出取消异常不会取消界面生命周期。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Notification_ThrownCancellationException_ShouldNotCancelLifecycle()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("CanceledNotify", CreateObject("CanceledNotifyRoot").transform);
            // 让“ui.BeforeUIShowEvent”的订阅者抛出预设异常，验证异常隔离与收尾。
            ui.BeforeUIShowEvent += () => throw new OperationCanceledException("subscriber-local cancellation");
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("OperationCanceledException: subscriber-local cancellation");

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认界面（ui）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, ui.State);
        }

        /// <summary>验证通知中显式取消调用方令牌仍会中断异步生命周期。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Notification_ExplicitTokenCancellation_ShouldStillCancelLifecycle()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("ExplicitCancelNotify", CreateObject("ExplicitCancelNotifyRoot").transform);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 初始化“observerCalls”为0，作为后续回调的观测基准。
            var observerCalls = 0;
            // 初始化“actionCalls”为0，作为后续回调的观测基准。
            var actionCalls = 0;
            // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIShowEvent += () =>
            {
                // 取消本轮操作，验证旧任务停止提交成功状态。
                cancellation.Cancel();
                // 抛出预设异常，验证调用方的失败传播与收尾行为。
                throw new InvalidOperationException("notification failed after explicit cancellation");
            };
            // 为“ui.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIShowEvent += () => observerCalls++;
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += _ =>
            {
                // 更新“actionCalls”的计数，记录回调实际执行次数。
                actionCalls++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("InvalidOperationException: notification failed after explicit cancellation");

            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.ShowAsync(cancellation.Token));
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“observerCalls”等于1。
            Assert.AreEqual(1, observerCalls);
            // 确认“actionCalls”等于0。
            Assert.AreEqual(0, actionCalls);
            // 确认界面（ui）的界面状态不同于显示终态。
            Assert.AreNotEqual(UIState.Showed, ui.State);
        }

        /// <summary>验证表现行为异常继续通过对应异步任务传播，不按通知异常处理。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Notification_ActionFailures_ShouldStillFailTheirOwnAsyncOperations()
        {
            // 创建“show”并配置测试所需的层级或表现组件。
            var show = CreateUI("FailedShowAction", CreateObject("FailedShowActionRoot").transform);
            // 创建“hide”并配置测试所需的层级或表现组件。
            var hide = CreateUI("FailedHideAction", CreateObject("FailedHideActionRoot").transform);
            // 提交“hide”的显示请求，触发实际生命周期。
            hide.Show();
            // 创建“showFailure”，隔离本用例的资源或观测数据。
            var showFailure = new InvalidOperationException("show action failed");
            // 创建“hideFailure”，隔离本用例的资源或观测数据。
            var hideFailure = new InvalidOperationException("hide action failed");
            // 初始化“afterShow”为0，作为后续回调的观测基准。
            var afterShow = 0;
            // 初始化“afterHide”为0，作为后续回调的观测基准。
            var afterHide = 0;
            // 让“show.BeforeUIShowEvent”的订阅者抛出预设异常，验证异常隔离与收尾。
            show.BeforeUIShowEvent += () => throw new ArgumentException("show notification failed");
            // 让“hide.BeforeUIHideEvent”的订阅者抛出预设异常，验证异常隔离与收尾。
            hide.BeforeUIHideEvent += () => throw new ArgumentException("hide notification failed");
            // 为“show.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            show.ShowAsyncActions += _ => UniTask.FromException(showFailure);
            // 为“hide.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            hide.HideAsyncActions += _ => UniTask.FromException(hideFailure);
            // 为“show.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            show.AfterUIShowedEvent += () => afterShow++;
            // 为“hide.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            hide.AfterUIHidedEvent += () => afterHide++;
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("ArgumentException: show notification failed");
            // 登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。
            ExpectNotificationError("ArgumentException: hide notification failed");

            // 启动并保存显示操作（showOperation），随后检查等待、取消与完成结果。
            var showOperation = CaptureException(show.ShowAsync(CancellationToken.None));
            // 启动并保存隐藏操作（hideOperation），随后检查等待、取消与完成结果。
            var hideOperation = CaptureException(hide.HideAsync(CancellationToken.None));
            // 等待“WaitForTask(Task.WhenAll(showOperation, hideOperation))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(showOperation, hideOperation));

            // 确认显示操作（showOperation）的异步结果仍引用“showFailure”。
            Assert.AreSame(showFailure, showOperation.Result);
            // 确认隐藏操作（hideOperation）的异步结果仍引用“hideFailure”。
            Assert.AreSame(hideFailure, hideOperation.Result);
            // 确认“afterShow”等于0。
            Assert.AreEqual(0, afterShow);
            // 确认“afterHide”等于0。
            Assert.AreEqual(0, afterHide);
        }

        /// <summary>为指定生命周期通知注册测试回调，并对状态通知筛选目标状态。</summary>
        /// <param name="ui">当前测试所操作或输出的 UIBase 宿主。</param>
        /// <param name="eventName">要订阅的生命周期事件名称。</param>
        /// <param name="eventState">状态通知需要匹配的目标状态。</param>
        /// <param name="callback">需要注册或触发的测试回调。</param>
        private static void SubscribeNotification(UIBase ui, string eventName, UIState eventState, Action callback)
        {
            // 按当前事件或测试类型选择对应的验证分支。
            switch (eventName)
            {
                case "OnStateChanged":
                    // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
                    ui.OnStateChanged += (_, state) =>
                    {
                        // 根据“state == eventState”选择对应测试路径。
                        if (state == eventState) callback();
                    };
                    // 结束当前匹配分支。
                    break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "BeforeUIShowEvent": ui.BeforeUIShowEvent += callback; break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "AfterUIShowedEvent": ui.AfterUIShowedEvent += callback; break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "BeforeUIHideEvent": ui.BeforeUIHideEvent += callback; break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "AfterUIHidedEvent": ui.AfterUIHidedEvent += callback; break;
                // 拒绝未知事件名称，避免测试订阅到错误的生命周期入口。
                default: throw new ArgumentOutOfRangeException(nameof(eventName));
            }
        }

        /// <summary>登记预期通知异常日志，避免将被验证的业务异常误判为测试失败。</summary>
        /// <param name="exceptionMessage">本次预期异常的原始消息。</param>
        private static void ExpectNotificationError(string exceptionMessage)
        {
            // Unity 将 InnerException 作为日志首行，包装异常的上下文位于展开后的异常栈中。
            LogAssert.Expect(LogType.Exception, exceptionMessage);
        }
    }
}
