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
        /// <summary>验证 Showing 通知内提交隐藏时先完成通知派发，再在同步返回前消费隐藏请求。</summary>
        [Test]
        public void Show_HideFromShowing_ShouldFinishNotificationAndDrainBeforeReturning()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("QueuedHide", CreateObject("QueuedHideRoot").transform);
            // 创建通知顺序记录（events），隔离本用例的资源或观测数据。
            var events = new List<string>();
            // 保存“frame”的基准值“Time.frameCount”，供后续比较或恢复。
            var frame = Time.frameCount;
            // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            ui.OnStateChanged += (_, state) =>
            {
                // 将本轮对象或观测值加入通知顺序记录（events），供统一清理或顺序断言使用。
                events.Add($"A:{state}");
                // 根据“state != UIState.Showing”选择对应测试路径。
                if (state != UIState.Showing) return;
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
                // 确认界面（ui）的界面状态等于显示过渡态。
                Assert.AreEqual(UIState.Showing, ui.State, "回调中的新请求不能立即改写当前状态。");
            };
            // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            ui.OnStateChanged += (_, state) =>
            {
                // 将本轮对象或观测值加入通知顺序记录（events），供统一清理或顺序断言使用。
                events.Add($"B:{state}");
                // 确认界面（ui）的界面状态等于“state”。
                Assert.AreEqual(state, ui.State, "后续订阅者应观察到本轮通知对应的状态。");
            };

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 确认通知顺序记录（events）等于“new[] { "A:Showing", "B:Showing", "A:Showed", "B:Showed", "A:Hiding", "B:Hiding", "A:Hided", "B:Hid…”。
            CollectionAssert.AreEqual(new[]
            {
                "A:Showing", "B:Showing", "A:Showed", "B:Showed",
                "A:Hiding", "B:Hiding", "A:Hided", "B:Hided"
            }, events);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
            // 确认“Time.frameCount”等于“frame”。
            Assert.AreEqual(frame, Time.frameCount, "消费队列不应等待下一帧。");
        }

        /// <summary>验证生命周期通知中的重入请求不会打断同次剩余订阅者。</summary>
        /// <param name="notification">触发重入请求的生命周期通知名称。</param>
        /// <param name="expectedState">通知订阅者应观察到的界面状态。</param>
        [TestCase("BeforeShow", UIState.Showing)]
        [TestCase("AfterShow", UIState.Showed)]
        [TestCase("BeforeHide", UIState.Hiding)]
        [TestCase("AfterHide", UIState.Hided)]
        public void Lifecycle_ReentrantRequest_ShouldNotInterruptRemainingSubscribers(string notification,
            UIState expectedState)
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI(notification, CreateObject(notification + "Root").transform);
            // 保存显示分支或操作（showing）的基准值“notification.EndsWith("Show")”，供后续比较或恢复。
            var showing = notification.EndsWith("Show");
            // 根据“!showing”选择对应测试路径。
            if (!showing) ui.Show();
            // 初始化“observed”为false，作为后续回调的观测基准。
            var observed = false;
            // 准备第一名订阅者：提出反向请求后确认当前通知状态尚未被排队请求改写。
            Action first = () =>
            {
                // 根据显示分支或操作（showing）选择对应测试路径。
                if (showing) ui.Hide();
                // 覆盖条件不成立时的另一条配置或清理路径。
                else ui.Show();
                // 确认界面（ui）的界面状态等于“expectedState”。
                Assert.AreEqual(expectedState, ui.State);
            };
            // 准备第二名订阅者：记录其仍被调用，并验证同次通知对应的状态。
            Action second = () =>
            {
                // 将“observed”设为true，建立当前测试条件。
                observed = true;
                // 确认界面（ui）的界面状态等于“expectedState”。
                Assert.AreEqual(expectedState, ui.State);
            };
            // 按当前事件或测试类型选择对应的验证分支。
            switch (notification)
            {
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "BeforeShow": ui.BeforeUIShowEvent += first; ui.BeforeUIShowEvent += second; break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "AfterShow": ui.AfterUIShowedEvent += first; ui.AfterUIShowedEvent += second; break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "BeforeHide": ui.BeforeUIHideEvent += first; ui.BeforeUIHideEvent += second; break;
                // 在该通知类型下依次注册两名订阅者，验证重入不会打断同次派发。
                case "AfterHide": ui.AfterUIHidedEvent += first; ui.AfterUIHidedEvent += second; break;
            }

            // 根据显示分支或操作（showing）选择对应测试路径。
            if (showing) ui.Show();
            // 覆盖条件不成立时的另一条配置或清理路径。
            else ui.Hide();

            // 确认“observed”为真。
            Assert.IsTrue(observed);
            // 确认界面（ui）的界面状态等于“showing ? UIState.Hided : UIState.Showed”。
            Assert.AreEqual(showing ? UIState.Hided : UIState.Showed, ui.State);
        }

        /// <summary>验证多个排队请求按提交顺序执行，并在执行时重新判断状态。</summary>
        [Test]
        public void Show_SeveralQueuedRequests_ShouldCheckStateAtExecutionAndPreserveOrder()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("QueueSource", CreateObject("QueueSourceRoot").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("QueueTarget", CreateObject("QueueTargetRoot").transform);
            // 创建“states”，隔离本用例的资源或观测数据。
            var states = new List<UIState>();
            // 为“target.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            target.OnStateChanged += (_, state) => states.Add(state);
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 提交目标对象（target）的显示请求，触发实际生命周期。
                target.Show();
                // 提交目标对象（target）的隐藏请求，检查子树联动与终态。
                target.Hide();
                // 提交目标对象（target）的显示请求，触发实际生命周期。
                target.Show();
                // 确认目标对象（target）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, target.State);
            };

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();

            // 确认“states”等于“new[] { UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided, UIState.Showing, UIState.Sh…”。
            CollectionAssert.AreEqual(new[]
            {
                UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided,
                UIState.Showing, UIState.Showed
            }, states);
        }

        /// <summary>验证子界面通知请求父隐藏时先结束子通知，再关闭整棵子树。</summary>
        [Test]
        public void Show_ChildRequestsParentHide_ShouldFinishChildNotificationBeforeClosingSubtree()
        {
            // 创建父界面（parent）并配置测试所需的层级或表现组件。
            var parent = CreateUI("QueuedParent", CreateObject("QueuedParentRoot").transform);
            // 创建子界面（child）并配置测试所需的层级或表现组件。
            var child = CreateUI("QueuedChild", parent.transform);
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, state) =>
            {
                // 根据“state == UIState.Showing”选择对应测试路径。
                if (state == UIState.Showing) parent.Hide();
            };
            // 为“child.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            child.OnStateChanged += (_, state) => Assert.AreEqual(state, child.State);

            // 提交子界面（child）的显示请求，触发实际生命周期。
            child.Show();

            // 确认父界面（parent）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, parent.State);
            // 确认子界面（child）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, child.State);
        }

        /// <summary>验证等待中的显示动画不阻挡后续隐藏，也不提交过期显示终态。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_PendingAnimation_ShouldNotBlockLaterHideOrCommitStaleShowed()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("InterruptQueuedAnimation", CreateObject("InterruptQueuedAnimationRoot").transform);
            // 创建“animation”，使动画完成时机由测试显式控制。
            var animation = new UniTaskCompletionSource();
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += _ => animation.Task;
            // 初始化“afterShow”为0，作为后续回调的观测基准。
            var afterShow = 0;
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () => afterShow++;
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 确认界面（ui）的界面状态等于显示过渡态。
            Assert.AreEqual(UIState.Showing, ui.State);

            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State, "挂起动画不能占住消费器。");
            // 确认异步操作（operation）的完成标志为假。
            Assert.IsFalse(operation.IsCompleted, "忽略取消的已启动行为仍需被观察。");
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            animation.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“afterShow”等于0。
            Assert.AreEqual(0, afterShow);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证异步显示完成通知提出的新请求仍在通知结束后执行。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_CompletionNotification_ShouldAlsoDeferNewRequests()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("CompletionNotification", CreateObject("CompletionNotificationRoot").transform);
            // 创建“animation”，使动画完成时机由测试显式控制。
            var animation = new UniTaskCompletionSource();
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += _ => animation.Task;
            // 初始化“observerCalled”为false，作为后续回调的观测基准。
            var observerCalled = false;
            // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            ui.OnStateChanged += (_, state) =>
            {
                // 根据“state == UIState.Showed”选择对应测试路径。
                if (state == UIState.Showed) ui.Hide();
            };
            // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            ui.OnStateChanged += (_, state) =>
            {
                // 根据“state != UIState.Showed”选择对应测试路径。
                if (state != UIState.Showed) return;
                // 将“observerCalled”设为true，建立当前测试条件。
                observerCalled = true;
                // 确认界面（ui）的界面状态等于显示终态。
                Assert.AreEqual(UIState.Showed, ui.State);
            };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.ShowAsync(CancellationToken.None));

            // 从消费器之外完成动画，验证恢复生命周期会重新进入调度器。
            animation.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result, "显示完成通知提出的 Hide 应在本段显示提交之后执行。");
            // 确认“observerCalled”为真。
            Assert.IsTrue(observerCalled);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证异步隐藏完成通知提出的新请求仍在通知结束后执行。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator HideAsync_CompletionNotification_ShouldAlsoDeferNewRequests()
        {
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("HideCompletion", CreateObject("HideCompletionRoot").transform);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 创建“animation”，使动画完成时机由测试显式控制。
            var animation = new UniTaskCompletionSource();
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += _ => animation.Task;
            // 初始化“observerCalled”为false，作为后续回调的观测基准。
            var observerCalled = false;
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += ui.Show;
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += () =>
            {
                // 将“observerCalled”设为true，建立当前测试条件。
                observerCalled = true;
                // 确认界面（ui）的界面状态等于隐藏终态。
                Assert.AreEqual(UIState.Hided, ui.State);
            };
            // 启动并保存异步操作（operation），随后检查等待、取消与完成结果。
            var operation = CaptureException(ui.HideAsync(CancellationToken.None));

            // 完成手动控制的挂起任务，让已启动行为能够收口。
            animation.TrySetResult();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果为空。
            Assert.IsNull(operation.Result);
            // 确认“observerCalled”为真。
            Assert.IsTrue(observerCalled);
            // 确认界面（ui）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, ui.State);
        }

        /// <summary>验证异步请求排队期间被取消时不启动生命周期或行为。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator QueuedAsyncRequest_CanceledBeforeExecution_ShouldHaveNoSideEffects()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("CancelQueueSource", CreateObject("CancelQueueSourceRoot").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("CancelQueueTarget", CreateObject("CancelQueueTargetRoot").transform);
            // 创建调用方取消源（cancellation），控制本次显隐的主动取消时机。
            using var cancellation = new CancellationTokenSource();
            // 初始化异步操作（operation）为空引用，作为后续回调的观测基准。
            Task<Exception> operation = null;
            // 初始化“notifications”为0，作为后续回调的观测基准。
            var notifications = 0;
            // 为“target.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            target.OnStateChanged += (_, _) => notifications++;
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 将异步操作（operation）设为“CaptureException(target.ShowAsync(cancellation.Token))”，建立当前测试条件。
                operation = CaptureException(target.ShowAsync(cancellation.Token));
                // 确认异步操作（operation）的完成标志为假。
                Assert.IsFalse(operation.IsCompleted);
                // 取消本轮操作，验证旧任务停止提交成功状态。
                cancellation.Cancel();
            };

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“notifications”等于0。
            Assert.AreEqual(0, notifications);
            // 确认目标对象（target）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, target.State);
        }

        /// <summary>验证排队异步请求的对象被销毁后报告取消并继续处理其他请求。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator QueuedAsyncRequest_DestroyedBeforeExecution_ShouldCancelAndContinueQueue()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("DestroyQueueSource", CreateObject("DestroyQueueSourceRoot").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("DestroyQueueTarget", CreateObject("DestroyQueueTargetRoot").transform);
            // 创建“next”并配置测试所需的层级或表现组件。
            var next = CreateUI("AfterDestroyedTarget", CreateObject("AfterDestroyedRoot").transform);
            // 初始化异步操作（operation）为空引用，作为后续回调的观测基准。
            Task<Exception> operation = null;
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 将异步操作（operation）设为“CaptureException(target.ShowAsync(CancellationToken.None))”，建立当前测试条件。
                operation = CaptureException(target.ShowAsync(CancellationToken.None));
                // 提交目标对象（target）的隐藏请求，检查子树联动与终态。
                target.Hide();
                // 移除指定 Unity 对象，触发真实销毁回调并验证清理结果。
                Object.DestroyImmediate(target.gameObject);
                // 提交“next”的显示请求，触发实际生命周期。
                next.Show();
            };

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();
            // 等待“WaitForTask(operation)”，再检查操作完成后的状态。
            yield return WaitForTask(operation);

            // 确认异步操作（operation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(operation.Result, Is.InstanceOf<OperationCanceledException>());
            // 确认“next”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, next.State);
        }

        /// <summary>验证空闲同步请求的行为异常返回调用方，同时继续消费已排队请求。</summary>
        [Test]
        public void Show_CallbackThrows_ShouldPropagateAndStillConsumeQueuedRequests()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("FaultQueueSource", CreateObject("FaultQueueSourceRoot").transform);
            // 创建“next”并配置测试所需的层级或表现组件。
            var next = CreateUI("FaultQueueNext", CreateObject("FaultQueueNextRoot").transform);
            // 创建“failure”，隔离本用例的资源或观测数据。
            var failure = new InvalidOperationException("request callback failed");
            // 为“source.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            source.ShowActions += () =>
            {
                // 提交“next”的显示请求，触发实际生命周期。
                next.Show();
                // 抛出预设异常，验证调用方的失败传播与收尾行为。
                throw failure;
            };

            // 确认“Assert.Throws<InvalidOperationException>(source.Show)”仍引用“failure”。
            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(source.Show));
            // 确认“next”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, next.State);
            // 提交“next”的隐藏请求，检查子树联动与终态。
            next.Hide();
            // 确认“next”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, next.State, "异常不能让消费标记永久保持忙碌。");
        }

        /// <summary>验证排队同步请求失败时记录异常日志并继续处理其余请求。</summary>
        [Test]
        public void DeferredSyncRequest_Throws_ShouldLogAndContinueOtherRequests()
        {
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("DeferredFaultSource", CreateObject("DeferredFaultSourceRoot").transform);
            // 创建“broken”并配置测试所需的层级或表现组件。
            var broken = CreateUI("DeferredFaultTarget", CreateObject("DeferredFaultTargetRoot").transform);
            // 创建“next”并配置测试所需的层级或表现组件。
            var next = CreateUI("DeferredFaultNext", CreateObject("DeferredFaultNextRoot").transform);
            // 让“broken.ShowActions”的订阅者抛出预设异常，验证异常隔离与收尾。
            broken.ShowActions += () => throw new InvalidOperationException("deferred request failed");
            // 为“source.BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.BeforeUIShowEvent += () =>
            {
                // 提交“broken”的显示请求，触发实际生命周期。
                broken.Show();
                // 提交“next”的显示请求，触发实际生命周期。
                next.Show();
            };
            // 登记本步骤预期产生的异常日志，区分被测失败与意外错误。
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: deferred request failed");

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();

            // 确认“next”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, next.State);
        }

        /// <summary>验证较长但有限的请求链通过队列完成，不递归叠加派发调用栈。</summary>
        [Test]
        public void Show_LongFiniteRequestChain_ShouldDrainWithoutRecursiveDispatch()
        {
            // 本用例验证消费器不递归入栈，显式提高循环保护上限以覆盖长但有限的业务链。
            using var limit = new LoopLimitScope(1024);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("FiniteChain", CreateObject("FiniteChainRoot").transform);
            // 初始化“transitions”为0，作为后续回调的观测基准。
            var transitions = 0;
            // 为“ui.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            ui.OnStateChanged += (_, state) =>
            {
                // 根据“state != UIState.Showed && state != UIState.Hided”选择对应测试路径。
                if (state != UIState.Showed && state != UIState.Hided) return;
                // 更新“transitions”的计数，记录回调实际执行次数。
                transitions++;
                // 根据“transitions >= 1024”选择对应测试路径。
                if (transitions >= 1024) return;
                // 根据“state == UIState.Showed”选择对应测试路径。
                if (state == UIState.Showed) ui.Hide();
                // 覆盖条件不成立时的另一条配置或清理路径。
                else ui.Show();
            };

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 确认“transitions”等于1024。
            Assert.AreEqual(1024, transitions);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }
    }
}
