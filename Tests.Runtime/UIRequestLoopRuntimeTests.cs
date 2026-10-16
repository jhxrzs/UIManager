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
    /// <summary>验证 UIBase 的请求链循环保护。</summary>
    public partial class Test_UIBase
    {

        /// <summary>验证互斥界面循环在第十一次显示前被阻止，调度器仍可处理独立请求。</summary>
        [Test]
        public void Loop_MutuallyExclusivePanels_ShouldStopBeforeEleventhShowAndKeepDispatcherUsable()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(10);
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("MutualLoopRoot");
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("LoopA", container.transform);
            // 创建第二项（second）并配置测试所需的层级或表现组件。
            var second = CreateUI("LoopB", container.transform);
            // 初始化“firstShows”为0，作为后续回调的观测基准。
            var firstShows = 0;
            // 初始化“secondShows”为0，作为后续回调的观测基准。
            var secondShows = 0;
            // 为“first.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            first.ShowActions += () => firstShows++;
            // 为“second.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            second.ShowActions += () => secondShows++;
            // 为“first.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            first.AfterUIShowedEvent += second.Show;
            // 为“second.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            second.AfterUIShowedEvent += first.Show;
            // 登记本步骤预期产生的异常日志，区分被测失败与意外错误。
            LogAssert.Expect(LogType.Exception, new Regex("UIRequestLoopException: UI循环调度次数超限"));

            // 提交首项（first）的显示请求，触发实际生命周期。
            first.Show();

            // 确认“firstShows”等于10。
            Assert.AreEqual(10, firstShows);
            // 确认“secondShows”等于10。
            Assert.AreEqual(10, secondShows);
            // 确认首项（first）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, first.State);
            // 确认第二项（second）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, second.State, "拒绝第11次请求前不得先隐藏当前互斥面板。");
            // 创建“independent”并配置测试所需的层级或表现组件。
            var independent = CreateUI("AfterLoop", CreateObject("AfterLoopRoot").transform);
            // 提交“independent”的显示请求，触发实际生命周期。
            independent.Show();
            // 确认“independent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, independent.State);
        }

        /// <summary>验证同步和异步同向行为共用循环计数，异常包含诊断信息。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Loop_MixedSyncAndAsync_ShouldShareCounterAndReturnDiagnosticException()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(2);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("MixedLoop", CreateObject("MixedLoopRoot").transform);
            // 创建“attempts”，隔离本用例的资源或观测数据。
            var attempts = new List<Task<Exception>>();
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“ui.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowActions += () => shows++;
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += ui.Hide;
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += () => attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 等待“WaitForTask(Task.WhenAll(attempts.ToArray()))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));

            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows);
            // 确认“attempts”的元素数量等于2。
            Assert.AreEqual(2, attempts.Count);
            // 确认“attempts[0]”的异步结果为空。
            Assert.IsNull(attempts[0].Result);
            // 确认“attempts[1]”的异步结果满足约束“Is.TypeOf<UIRequestLoopException>()”。
            Assert.That(attempts[1].Result, Is.TypeOf<UIRequestLoopException>());
            // 保存“failure”的基准值“(UIRequestLoopException)attempts[1]”的异步结果，供后续比较或恢复。
            var failure = (UIRequestLoopException)attempts[1].Result;
            // 确认“failure.Panel”仍引用界面（ui）。
            Assert.AreSame(ui, failure.Panel);
            // 确认“failure.IsShow”为真。
            Assert.IsTrue(failure.IsShow);
            // 确认“failure.ExecutionLimit”等于2。
            Assert.AreEqual(2, failure.ExecutionLimit);
            // 确认“failure.PanelPath”包含“"MixedLoopRoot/MixedLoop"”。
            StringAssert.Contains("MixedLoopRoot/MixedLoop", failure.PanelPath);
            // 确认“failure.RequestPath”包含“".Hide"”。
            StringAssert.Contains(".Hide", failure.RequestPath);
            // 确认“failure.RequestPath”包含“"[本次被阻止]"”。
            StringAssert.Contains("[本次被阻止]", failure.RequestPath);
            // 确认“failure.Message”包含“"第 3 次已被阻止"”。
            StringAssert.Contains("第 3 次已被阻止", failure.Message);
            // 确认“failure.RequestOrigin”包含“nameof(Loop_MixedSyncAndAsync_ShouldShareCounterAndReturnDiagnosticException)”。
            StringAssert.Contains(nameof(Loop_MixedSyncAndAsync_ShouldShareCounterAndReturnDiagnosticException),
                failure.RequestOrigin);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证低于限制的有限重复请求正常完成。</summary>
        [Test]
        public void Loop_FiniteRepetitionBelowLimit_ShouldCompleteNormally()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(10);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("FiniteLoop", CreateObject("FiniteLoopRoot").transform);
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () =>
            {
                // 更新显示次数（shows）的计数，记录回调实际执行次数。
                shows++;
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
            };
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += () =>
            {
                // 根据“shows < 3”选择对应测试路径。
                if (shows < 3) ui.Show();
            };

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();

            // 确认显示次数（shows）等于3。
            Assert.AreEqual(3, shows);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证独立外部请求各自建立新的循环计数链。</summary>
        [Test]
        public void Loop_IndependentRequests_ShouldStartFreshChains()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("IndependentLoop", CreateObject("IndependentLoopRoot").transform);
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“ui.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowActions += () => shows++;

            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var index = 0; index < 20; index++)
            {
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
            }

            // 确认显示次数（shows）等于20。
            Assert.AreEqual(20, shows);
        }

        /// <summary>验证同一来源的并列请求分支不会累计彼此的执行次数。</summary>
        [Test]
        public void Loop_ParallelBranches_ShouldNotAccumulateEachOthersExecutions()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建原来源对象（source）并配置测试所需的层级或表现组件。
            var source = CreateUI("BranchSource", CreateObject("BranchSourceRoot").transform);
            // 创建目标对象（target）并配置测试所需的层级或表现组件。
            var target = CreateUI("BranchTarget", CreateObject("BranchTargetRoot").transform);
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“target.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            target.ShowActions += () => shows++;
            // 为“source.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            source.AfterUIShowedEvent += () =>
            {
                // 提交目标对象（target）的显示请求，触发实际生命周期。
                target.Show();
                // 提交目标对象（target）的隐藏请求，检查子树联动与终态。
                target.Hide();
                // 提交目标对象（target）的显示请求，触发实际生命周期。
                target.Show();
            };

            // 提交原来源对象（source）的显示请求，触发实际生命周期。
            source.Show();

            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows, "并列请求共享父请求，但不互为祖先。");
            // 确认目标对象（target）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, target.State);
        }

        /// <summary>验证 Showed 状态下短路的重复显示不计入实际执行次数。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Loop_AlreadyShowedRepeatedRequest_ShouldNotCountAsExecution()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("NoOpLoop", CreateObject("NoOpLoopRoot").transform);
            // 初始化“repeated”为空引用，作为后续回调的观测基准。
            Task<Exception> repeated = null;
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“ui.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowActions += () => shows++;
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () =>
            {
                // 提交界面（ui）的显示请求，触发实际生命周期。
                ui.Show();
                // 将“repeated”设为“CaptureException(ui.ShowAsync(CancellationToken.None))”，建立当前测试条件。
                repeated = CaptureException(ui.ShowAsync(CancellationToken.None));
            };

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 等待“WaitForTask(repeated)”，再检查操作完成后的状态。
            yield return WaitForTask(repeated);

            // 确认“repeated”的异步结果为空。
            Assert.IsNull(repeated.Result);
            // 确认显示次数（shows）等于1。
            Assert.AreEqual(1, shows);
        }

        /// <summary>验证被准入拒绝的重复请求不触发循环上限。</summary>
        [Test]
        public void Loop_AdmissionRejectedRepeatedRequest_ShouldNotTriggerGuard()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建层级容器（container）并配置测试所需的层级或表现组件。
            var container = CreateObject("RejectedLoopRoot");
            // 创建低优先级界面（low）并配置测试所需的层级或表现组件。
            var low = CreateUI("RejectedLoopLow", container.transform);
            // 创建高优先级界面（high）并配置测试所需的层级或表现组件。
            var high = CreateUI("RejectedLoopHigh", container.transform);
            // 将高优先级界面（high）的互斥优先级设为1，建立当前测试条件。
            high.MutexPriority = 1;
            // 初始化“lowShows”为0，作为后续回调的观测基准。
            var lowShows = 0;
            // 为“low.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            low.ShowActions += () => lowShows++;
            // 为“low.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            low.AfterUIShowedEvent += high.Show;
            // 为“high.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            high.AfterUIShowedEvent += low.Show;

            // 提交低优先级界面（low）的显示请求，触发实际生命周期。
            low.Show();

            // 确认“lowShows”等于1。
            Assert.AreEqual(1, lowShows);
            // 确认低优先级界面（low）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, low.State);
            // 确认高优先级界面（high）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, high.State);
        }

        /// <summary>验证名称相同的不同 UI 实例不会共享循环执行计数。</summary>
        [Test]
        public void Loop_SameNameDifferentInstances_ShouldNotMatch()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建首项（first）并配置测试所需的层级或表现组件。
            var first = CreateUI("SameName", CreateObject("FirstInstanceRoot").transform);
            // 创建第二项（second）并配置测试所需的层级或表现组件。
            var second = CreateUI("SameName", CreateObject("SecondInstanceRoot").transform);
            // 为“first.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            first.AfterUIShowedEvent += second.Show;

            // 提交首项（first）的显示请求，触发实际生命周期。
            first.Show();

            // 确认首项（first）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, first.State);
            // 确认第二项（second）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, second.State);
        }

        /// <summary>验证跨帧动画即使曾清空队列也保留请求来源链。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Loop_AcrossAnimationFrames_ShouldRetainAncestryWhenQueueWasEmpty()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(2);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("AsyncFrameLoop", CreateObject("AsyncFrameLoopRoot").transform);
            // 创建“attempts”，隔离本用例的资源或观测数据。
            var attempts = new List<Task<Exception>>();
            // 初始化显示次数（shows）为0，作为后续回调的观测基准。
            var shows = 0;
            // 为“ui.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.ShowAsyncActions += _ =>
            {
                // 更新显示次数（shows）的计数，记录回调实际执行次数。
                shows++;
                // 返回“UniTask.NextFrame()”，交给调用方继续配置或断言。
                return UniTask.NextFrame();
            };
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += ui.Hide;
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += () => attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));
            // 将本轮对象或观测值加入“attempts”，供统一清理或顺序断言使用。
            attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));
            // 确认显示次数（shows）等于1。
            Assert.AreEqual(1, shows);

            // 保存等待截止时间（deadline）的基准值“Time.realtimeSinceStartup + TestTimeout”，供后续比较或恢复。
            var deadline = Time.realtimeSinceStartup + TestTimeout;
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (attempts.Count < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            // 确认“attempts”的元素数量等于3。
            Assert.AreEqual(3, attempts.Count, "跨帧后仍须在第3次显示前触发保护。");
            // 等待“WaitForTask(Task.WhenAll(attempts.ToArray()))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));

            // 确认“attempts[0]”的异步结果为空。
            Assert.IsNull(attempts[0].Result);
            // 确认“attempts[1]”的异步结果为空。
            Assert.IsNull(attempts[1].Result);
            // 确认“attempts[2]”的异步结果满足约束“Is.TypeOf<UIRequestLoopException>()”。
            Assert.That(attempts[2].Result, Is.TypeOf<UIRequestLoopException>());
            // 确认显示次数（shows）等于2。
            Assert.AreEqual(2, shows);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证隐藏方向具有独立于显示方向的执行计数。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Loop_HideIntent_ShouldHaveItsOwnCounter()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("HideLoop", CreateObject("HideLoopRoot").transform);
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 初始化隐藏次数（hides）为0，作为后续回调的观测基准。
            var hides = 0;
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += _ =>
            {
                // 更新隐藏次数（hides）的计数，记录回调实际执行次数。
                hides++;
                // 返回“UniTask.NextFrame()”，交给调用方继续配置或断言。
                return UniTask.NextFrame();
            };
            // 初始化“repeated”为空引用，作为后续回调的观测基准。
            Task<Exception> repeated = null;
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += ui.Show;
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () => repeated = CaptureException(ui.HideAsync(CancellationToken.None));
            // 启动并保存原始基准（original），随后检查等待、取消与完成结果。
            var original = CaptureException(ui.HideAsync(CancellationToken.None));

            // 保存等待截止时间（deadline）的基准值“Time.realtimeSinceStartup + TestTimeout”，供后续比较或恢复。
            var deadline = Time.realtimeSinceStartup + TestTimeout;
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (repeated == null && Time.realtimeSinceStartup < deadline) yield return null;
            // 确认“repeated”存在。
            Assert.IsNotNull(repeated);
            // 等待“WaitForTask(Task.WhenAll(original, repeated))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(original, repeated));

            // 确认原始基准（original）的异步结果为空。
            Assert.IsNull(original.Result);
            // 确认“repeated”的异步结果满足约束“Is.TypeOf<UIRequestLoopException>()”。
            Assert.That(repeated.Result, Is.TypeOf<UIRequestLoopException>());
            // 确认“((UIRequestLoopException)repeated.Result).IsShow”为假。
            Assert.IsFalse(((UIRequestLoopException)repeated.Result).IsShow);
            // 确认隐藏次数（hides）等于1。
            Assert.AreEqual(1, hides);
            // 确认界面（ui）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, ui.State);
        }

        /// <summary>验证超限显示在开始前被拒绝，不取消既有隐藏或丢失其完成通知。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Loop_BlockedShow_ShouldNotCancelAnExistingHideOrLoseItsCompletion()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(1);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("PendingHideLoop", CreateObject("PendingHideLoopRoot").transform);
            // 创建“hideAnimation”，使动画完成时机由测试显式控制。
            var hideAnimation = new UniTaskCompletionSource();
            // 初始化“hideToken”为“default(CancellationToken)”，作为后续回调的观测基准。
            var hideToken = default(CancellationToken);
            // 初始化“hide”为空引用，作为后续回调的观测基准。
            Task<Exception> hide = null;
            // 初始化“blockedShow”为空引用，作为后续回调的观测基准。
            Task<Exception> blockedShow = null;
            // 为“ui.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            ui.HideAsyncActions += token =>
            {
                // 将“hideToken”设为操作令牌（token），建立当前测试条件。
                hideToken = token;
                // 返回“hideAnimation.Task”，交给调用方继续配置或断言。
                return hideAnimation.Task;
            };
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () => hide = CaptureException(ui.HideAsync(CancellationToken.None));
            // 为“ui.BeforeUIHideEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.BeforeUIHideEvent += () => blockedShow = CaptureException(ui.ShowAsync(CancellationToken.None));

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 确认“blockedShow”存在。
            Assert.IsNotNull(blockedShow);
            // 确认“blockedShow”的完成标志为真。
            Assert.IsTrue(blockedShow.IsCompleted, "被拒绝请求的任务必须完成，不能留在等待中。");
            // 确认“blockedShow”的异步结果满足约束“Is.TypeOf<UIRequestLoopException>()”。
            Assert.That(blockedShow.Result, Is.TypeOf<UIRequestLoopException>());
            // 确认“hideToken”的取消标志为假。
            Assert.IsFalse(hideToken.IsCancellationRequested, "超限检查必须早于取消现有操作。");
            // 确认界面（ui）的界面状态等于隐藏过渡态。
            Assert.AreEqual(UIState.Hiding, ui.State);
            // 完成手动控制的挂起任务，让已启动行为能够收口。
            hideAnimation.TrySetResult();
            // 等待“WaitForTask(hide)”，再检查操作完成后的状态。
            yield return WaitForTask(hide);

            // 确认“hide”的异步结果为空。
            Assert.IsNull(hide.Result);
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State);
        }

        /// <summary>验证修改循环上限仅影响之后创建的根请求链。</summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Loop_ConfigurationChange_ShouldOnlyAffectNewRootChains()
        {
            // 创建“limit”，隔离本用例的资源或观测数据。
            using var limit = new LoopLimitScope(2);
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("SnapshotLoop", CreateObject("SnapshotLoopRoot").transform);
            // 创建“attempts”，隔离本用例的资源或观测数据。
            var attempts = new List<Task<Exception>>();
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () =>
            {
                // 将“UIOperationSettings.MaxExecutionsPerBehavior”设为1，建立当前测试条件。
                UIOperationSettings.MaxExecutionsPerBehavior = 1;
                // 提交界面（ui）的隐藏请求，检查子树联动与终态。
                ui.Hide();
            };
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += () => attempts.Add(CaptureException(ui.ShowAsync(CancellationToken.None)));

            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 等待“WaitForTask(Task.WhenAll(attempts.ToArray()))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));
            // 确认“attempts”的元素数量等于2。
            Assert.AreEqual(2, attempts.Count);
            // 确认“attempts[0]”的异步结果为空。
            Assert.IsNull(attempts[0].Result);
            // 确认“((UIRequestLoopException)attempts[1].Result).ExecutionLimit”等于2。
            Assert.AreEqual(2, ((UIRequestLoopException)attempts[1].Result).ExecutionLimit);

            // 清空“attempts”，防止上一步数据影响后续观察。
            attempts.Clear();
            // 提交界面（ui）的显示请求，触发实际生命周期。
            ui.Show();
            // 等待“WaitForTask(Task.WhenAll(attempts.ToArray()))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(attempts.ToArray()));
            // 确认“attempts”的元素数量等于1。
            Assert.AreEqual(1, attempts.Count);
            // 确认“((UIRequestLoopException)attempts[0].Result).ExecutionLimit”等于1。
            Assert.AreEqual(1, ((UIRequestLoopException)attempts[0].Result).ExecutionLimit);
        }

        /// <summary>验证无效循环上限抛出参数错误且保留原配置。</summary>
        /// <param name="value">应被拒绝的非正数执行上限。</param>
        [TestCase(0)]
        [TestCase(-1)]
        public void Loop_InvalidLimit_ShouldRejectWithoutChangingConfiguration(int value)
        {
            // 保存原始基准（original）的基准值“UIOperationSettings.MaxExecutionsPerBehavior”，供后续比较或恢复。
            var original = UIOperationSettings.MaxExecutionsPerBehavior;
            // 确认该调用抛出ArgumentOutOfRangeException。
            Assert.Throws<ArgumentOutOfRangeException>(() => UIOperationSettings.MaxExecutionsPerBehavior = value);
            // 确认“UIOperationSettings.MaxExecutionsPerBehavior”等于原始基准（original）。
            Assert.AreEqual(original, UIOperationSettings.MaxExecutionsPerBehavior);
        }
    }
}
