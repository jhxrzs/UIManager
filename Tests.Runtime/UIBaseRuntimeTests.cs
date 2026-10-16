using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace JH.Test_UIManager
{
    /// <summary>
    ///  UIBase异步生命周期、取消传播和动态层级关系的PlayMode回归测试。
    /// </summary>
    public partial class Test_UIBase
    {
        /// <summary>
        ///  单个异步用例允许等待的最长实时秒数。
        /// </summary>
        private const float TestTimeout = 3f;

        /// <summary>
        ///  当前用例创建的全部GameObject，供测试结束时统一清理。
        /// </summary>
        private readonly List<GameObject> _createdObjects = new();

        /// <summary>
        ///  每个测试结束后销毁临时层级，避免对象进入后续用例的兄弟关系查询。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // 反向销毁可优先清理子节点，并避免父节点销毁后再次处理已销毁对象。
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                // Unity对象已经随父节点销毁时会表现为false，不再重复销毁。
                var createdObject = _createdObjects[i];
                // 根据“createdObject”选择对应测试路径。
                if (createdObject) Object.DestroyImmediate(createdObject);
            }

            // 清空托管引用，保证下个用例从全新对象集合开始。
            _createdObjects.Clear();
        }

        /// <summary>
        ///  验证异步显示和隐藏在全部处理器正常完成时分别提交一次After生命周期。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAndHideAsync_CompletedActions_ShouldReachFinalStatesAndInvokeAfterOnce()
        {
            // 放入非UI容器，避免测试场景中的其他根UI参与兄弟关系。
            var container = CreateObject("NormalAsyncRoot");
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("NormalAsyncUI", container.transform);
            // 初始化“showActionCount”为0，作为后续回调的观测基准。
            var showActionCount = 0;
            // 初始化“hideActionCount”为0，作为后续回调的观测基准。
            var hideActionCount = 0;
            // 初始化“afterShowCount”为0，作为后续回调的观测基准。
            var afterShowCount = 0;
            // 初始化“afterHideCount”为0，作为后续回调的观测基准。
            var afterHideCount = 0;

            // 使用立即完成任务覆盖异步显示处理器的正常组合路径。
            ui.ShowAsyncActions += _ =>
            {
                // 更新“showActionCount”的计数，记录回调实际执行次数。
                showActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 使用立即完成任务覆盖异步隐藏处理器的正常组合路径。
            ui.HideAsyncActions += _ =>
            {
                // 更新“hideActionCount”的计数，记录回调实际执行次数。
                hideActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 分别统计两个完整生命周期的After通知次数。
            ui.AfterUIShowedEvent += () => afterShowCount++;
            // 为“ui.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIHidedEvent += () => afterHideCount++;

            // 等待显示流程，任何未捕获异常都会由结果断言报告。
            Task<Exception> showOperation = CaptureException(ui.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(showOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(showOperation);

            // 显示应无异常完成，并且只运行一次处理器与After回调。
            Assert.IsNull(showOperation.Result, $"异步显示不应产生异常：{showOperation.Result}");
            // 确认界面（ui）的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, ui.State, "异步显示完成后状态应为Showed");
            // 确认“showActionCount”等于1。
            Assert.AreEqual(1, showActionCount, "显示处理器应且只应执行一次");
            // 确认“afterShowCount”等于1。
            Assert.AreEqual(1, afterShowCount, "显示After事件应且只应执行一次");

            // 在同一UI上继续等待隐藏流程，覆盖隐藏侧的无令牌WhenAll调用。
            Task<Exception> hideOperation = CaptureException(ui.HideAsync(CancellationToken.None));
            // 等待“WaitForTask(hideOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(hideOperation);

            // 隐藏应无异常完成，并且不应重复显示侧生命周期。
            Assert.IsNull(hideOperation.Result, $"异步隐藏不应产生异常：{hideOperation.Result}");
            // 确认界面（ui）的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, ui.State, "异步隐藏完成后状态应为Hided");
            // 确认“hideActionCount”等于1。
            Assert.AreEqual(1, hideActionCount, "隐藏处理器应且只应执行一次");
            // 确认“afterHideCount”等于1。
            Assert.AreEqual(1, afterHideCount, "隐藏After事件应且只应执行一次");
            // 确认“afterShowCount”等于1。
            Assert.AreEqual(1, afterShowCount, "隐藏流程不应重复触发显示After事件");
        }

        /// <summary>
        ///  验证预取消的显示和隐藏不会改变状态、派发事件或启动用户处理器。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAndHideAsync_PreCanceled_ShouldHaveNoSideEffects()
        {
            // 两个对象分别放入非UI容器，保证状态准备过程互不触发兄弟互斥。
            var showContainer = CreateObject("PreCanceledShowRoot");
            // 创建“hideContainer”并配置测试所需的层级或表现组件。
            var hideContainer = CreateObject("PreCanceledHideRoot");

            // 显示测试对象保持默认Hided状态。
            var showUI = CreateUI("PreCanceledShowUI", showContainer.transform);

            // 隐藏测试对象先同步进入Showed，再开始记录待验证副作用。
            var hideUI = CreateUI("PreCanceledHideUI", hideContainer.transform);
            // 提交“hideUI”的显示请求，触发实际生命周期。
            hideUI.Show();

            // 分别记录显示侧和隐藏侧所有公开可观察副作用。
            var showBeforeCount = 0;
            // 初始化“showAfterCount”为0，作为后续回调的观测基准。
            var showAfterCount = 0;
            // 初始化“showActionCount”为0，作为后续回调的观测基准。
            var showActionCount = 0;
            // 初始化“showStateChangedCount”为0，作为后续回调的观测基准。
            var showStateChangedCount = 0;
            // 初始化“hideBeforeCount”为0，作为后续回调的观测基准。
            var hideBeforeCount = 0;
            // 初始化“hideAfterCount”为0，作为后续回调的观测基准。
            var hideAfterCount = 0;
            // 初始化“hideActionCount”为0，作为后续回调的观测基准。
            var hideActionCount = 0;
            // 初始化“hideStateChangedCount”为0，作为后续回调的观测基准。
            var hideStateChangedCount = 0;

            // 预取消时以下显示委托均不得被调用。
            showUI.BeforeUIShowEvent += () => showBeforeCount++;
            // 为“showUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            showUI.AfterUIShowedEvent += () => showAfterCount++;
            // 为“showUI.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            showUI.OnStateChanged += (_, _) => showStateChangedCount++;
            // 为“showUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            showUI.ShowAsyncActions += _ =>
            {
                // 更新“showActionCount”的计数，记录回调实际执行次数。
                showActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 预取消时以下隐藏委托均不得被调用。
            hideUI.BeforeUIHideEvent += () => hideBeforeCount++;
            // 为“hideUI.AfterUIHidedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            hideUI.AfterUIHidedEvent += () => hideAfterCount++;
            // 为“hideUI.OnStateChanged”追加观测或重入回调，验证通知次数及执行顺序。
            hideUI.OnStateChanged += (_, _) => hideStateChangedCount++;
            // 为“hideUI.HideAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            hideUI.HideAsyncActions += _ =>
            {
                // 更新“hideActionCount”的计数，记录回调实际执行次数。
                hideActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 两个令牌都在调用API之前取消，验证入口处的取消检查。
            using var showTokenSource = new CancellationTokenSource();
            // 创建“hideTokenSource”，控制本次显隐的主动取消时机。
            using var hideTokenSource = new CancellationTokenSource();
            // 取消本轮操作，验证旧任务停止提交成功状态。
            showTokenSource.Cancel();
            // 取消本轮操作，验证旧任务停止提交成功状态。
            hideTokenSource.Cancel();

            // 同时观察显示和隐藏结果，避免依赖同步完成细节。
            Task<Exception> showOperation = CaptureException(showUI.ShowAsync(showTokenSource.Token));
            // 启动并保存隐藏操作（hideOperation），随后检查等待、取消与完成结果。
            Task<Exception> hideOperation = CaptureException(hideUI.HideAsync(hideTokenSource.Token));
            // 等待“WaitForTask(Task.WhenAll(showOperation, hideOperation))”，再检查操作完成后的状态。
            yield return WaitForTask(Task.WhenAll(showOperation, hideOperation));

            // 两个API都必须通过标准取消异常结束。
            Assert.That(showOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "预取消显示应传播OperationCanceledException");
            // 确认隐藏操作（hideOperation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(hideOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "预取消隐藏应传播OperationCanceledException");

            // 显示对象必须维持调用前状态，且没有任何生命周期副作用。
            Assert.AreEqual(UIState.Hided, showUI.State, "预取消显示不应改变原始状态");
            // 确认“showBeforeCount”等于0。
            Assert.AreEqual(0, showBeforeCount, "预取消显示不应触发Before事件");
            // 确认“showAfterCount”等于0。
            Assert.AreEqual(0, showAfterCount, "预取消显示不应触发After事件");
            // 确认“showActionCount”等于0。
            Assert.AreEqual(0, showActionCount, "预取消显示不应启动用户处理器");
            // 确认“showStateChangedCount”等于0。
            Assert.AreEqual(0, showStateChangedCount, "预取消显示不应触发状态变化");

            // 隐藏对象也必须维持调用前状态，且没有任何生命周期副作用。
            Assert.AreEqual(UIState.Showed, hideUI.State, "预取消隐藏不应改变原始状态");
            // 确认“hideBeforeCount”等于0。
            Assert.AreEqual(0, hideBeforeCount, "预取消隐藏不应触发Before事件");
            // 确认“hideAfterCount”等于0。
            Assert.AreEqual(0, hideAfterCount, "预取消隐藏不应触发After事件");
            // 确认“hideActionCount”等于0。
            Assert.AreEqual(0, hideActionCount, "预取消隐藏不应启动用户处理器");
            // 确认“hideStateChangedCount”等于0。
            Assert.AreEqual(0, hideStateChangedCount, "预取消隐藏不应触发状态变化");
        }

        /// <summary>
        ///  验证忽略令牌的处理器不会被WhenAll提前遗弃，结束后仍应传播取消且不提交After状态。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_HandlerIgnoresToken_ShouldWaitThenThrowAndSkipAfter()
        {
            // 使用非UI容器隔离关系，并用外部开关控制故意不观察取消令牌的任务。
            var container = CreateObject("IgnoreTokenRoot");
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("IgnoreTokenUI", container.transform);
            // 初始化“releaseHandler”为false，作为后续回调的观测基准。
            var releaseHandler = false;
            // 初始化“handlerStarted”为false，作为后续回调的观测基准。
            var handlerStarted = false;
            // 初始化“afterShowCount”为0，作为后续回调的观测基准。
            var afterShowCount = 0;

            // 该处理器只等待releaseHandler，刻意忽略UI传入的令牌。
            ui.ShowAsyncActions += _ =>
            {
                // 将“handlerStarted”设为true，建立当前测试条件。
                handlerStarted = true;
                // 返回“UniTask.WaitUntil(() => releaseHandler)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseHandler);
            };
            // 为“ui.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            ui.AfterUIShowedEvent += () => afterShowCount++;

            // 启动显示后等待一帧，确保处理器已经进入组合任务。
            using var tokenSource = new CancellationTokenSource();
            // 启动并保存显示操作（showOperation），随后检查等待、取消与完成结果。
            Task<Exception> showOperation = CaptureException(ui.ShowAsync(tokenSource.Token));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 取消操作，但暂时不允许不合作的处理器完成。
            tokenSource.Cancel();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 保存“completedBeforeHandlerReleased”的基准值显示操作（showOperation）的完成标志，供后续比较或恢复。
            var completedBeforeHandlerReleased = showOperation.IsCompleted;

            // 无论中间断言结果如何都先释放处理器，避免给全局调度器留下永久等待项。
            releaseHandler = true;
            // 等待“WaitForTask(showOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(showOperation);

            // WhenAll只组合任务，不能因为外部令牌取消就提前结束等待。
            Assert.IsTrue(handlerStarted, "显示处理器应在取消前启动");
            // 确认“completedBeforeHandlerReleased”为假。
            Assert.IsFalse(completedBeforeHandlerReleased, "处理器未结束时组合任务不得提前完成");

            // 处理器结束后，UI生命周期检查令牌并阻止完成状态和After事件。
            Assert.That(showOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "忽略令牌的处理器结束后仍应传播操作取消");
            // 确认“afterShowCount”等于0。
            Assert.AreEqual(0, afterShowCount, "已取消显示不得触发After事件");
            // 确认界面（ui）的界面状态不同于显示终态。
            Assert.AreNotEqual(UIState.Showed, ui.State, "已取消显示不得提交Showed状态");
        }

        /// <summary>
        ///  验证内部父UI的独立取消会穿过关系组合传播到最外层子UI。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_ParentActionCanceled_ShouldPropagateThroughChildComposition()
        {
            // 非UI容器隔离测试层级，避免Scene根节点参与兄弟查询。
            var container = CreateObject("NestedCancellationRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("NestedCancellationParent", container.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("NestedCancellationChild", parentUI.transform);
            // 初始化“parentAfterCount”为0，作为后续回调的观测基准。
            var parentAfterCount = 0;
            // 初始化“childAfterCount”为0，作为后续回调的观测基准。
            var childAfterCount = 0;

            // 父UI使用独立令牌取消，子UI调用方令牌保持None。
            using var parentActionTokenSource = new CancellationTokenSource();
            // 为“parentUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.ShowAsyncActions += _ => WaitUntilCanceled(parentActionTokenSource.Token);
            // 为“childUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.ShowAsyncActions += _ => UniTask.CompletedTask;
            // 为“parentUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.AfterUIShowedEvent += () => parentAfterCount++;
            // 为“childUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            childUI.AfterUIShowedEvent += () => childAfterCount++;

            // 启动子UI会递归启动直接父UI的异步显示。
            Task<Exception> childOperation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 仅取消父UI内部处理器，验证取消不能被父UI公开入口吞掉。
            parentActionTokenSource.Cancel();
            // 等待“WaitForTask(childOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(childOperation);

            // 内部取消必须使整条组合链失败，任何层级都不能提交After。
            Assert.That(childOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "父UI内部取消必须传播到子UI调用方");
            // 确认“parentAfterCount”等于0。
            Assert.AreEqual(0, parentAfterCount, "取消后的父UI不得触发After事件");
            // 确认“childAfterCount”等于0。
            Assert.AreEqual(0, childAfterCount, "父UI取消后的子UI不得触发After事件");
        }

        /// <summary>
        ///  验证三层UI按根到直接父节点同步启动关系调度，每一层生命周期均执行一次。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_ThreeLevelHierarchy_ShouldInvokeEachLevelOnce()
        {
            // 构建容器、祖父UI、父UI、子UI四级Transform结构。
            var container = CreateObject("ThreeLevelRoot");
            // 创建“grandParentUI”并配置测试所需的层级或表现组件。
            var grandParentUI = CreateUI("GrandParentUI", container.transform);
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("ParentUI", grandParentUI.transform);
            // 创建“childUI”并配置测试所需的层级或表现组件。
            var childUI = CreateUI("ChildUI", parentUI.transform);
            // 创建“panels”，隔离本用例的资源或观测数据。
            var panels = new[] { grandParentUI, parentUI, childUI };
            // 创建“beforeCounts”，隔离本用例的资源或观测数据。
            var beforeCounts = new int[panels.Length];
            // 创建“actionCounts”，隔离本用例的资源或观测数据。
            var actionCounts = new int[panels.Length];
            // 创建“afterCounts”，隔离本用例的资源或观测数据。
            var afterCounts = new int[panels.Length];
            // 初始化“releaseGrandParent”为false，作为后续回调的观测基准。
            var releaseGrandParent = false;

            // 为每一层注册独立计数器，避免仅凭最终状态遗漏重复调用。
            for (var i = 0; i < panels.Length; i++)
            {
                // 保存“index”的基准值“i”，供后续比较或恢复。
                var index = i;
                // 为“panels[index].BeforeUIShowEvent”追加观测或重入回调，验证通知次数及执行顺序。
                panels[index].BeforeUIShowEvent += () => beforeCounts[index]++;
                // 为“panels[index].ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
                panels[index].ShowAsyncActions += _ =>
                {
                    // 更新“actionCounts[index]”的计数，记录回调实际执行次数。
                    actionCounts[index]++;

                    // 根祖先保持挂起，用于证明下游调度不等待动画、祖先也不会被重复启动。
                    return index == 0
                        ? UniTask.WaitUntil(() => releaseGrandParent)
                        : UniTask.CompletedTask;
                };
                // 为“panels[index].AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
                panels[index].AfterUIShowedEvent += () => afterCounts[index]++;
            }

            // 只请求最深层子UI，完整祖先快照应先启动根祖先。
            Task<Exception> showOperation = CaptureException(childUI.ShowAsync(CancellationToken.None));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 先记录挂起期间的执行次数，再释放闸门，避免失败断言遗留永久等待项。
            var grandParentCountBeforeRelease = actionCounts[0];
            // 保存“parentCountBeforeRelease”的基准值“actionCounts[1]”，供后续比较或恢复。
            var parentCountBeforeRelease = actionCounts[1];
            // 将“releaseGrandParent”设为true，建立当前测试条件。
            releaseGrandParent = true;

            // 根祖先只启动一次，直接父UI也必须已经启动。
            Assert.AreEqual(1, grandParentCountBeforeRelease,
                "根祖先挂起期间只能启动一次显示处理器");
            // 确认“parentCountBeforeRelease”等于1。
            Assert.AreEqual(1, parentCountBeforeRelease,
                "直接父UI的关系调度不得等待根祖先动画完成");

            // 解除根祖先闸门后再等待完整父链收口。
            yield return WaitForTask(showOperation);

            // 整条三层生命周期应无异常完成。
            Assert.IsNull(showOperation.Result, $"三层异步显示不应产生异常：{showOperation.Result}");
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = 0; i < panels.Length; i++)
            {
                // 每层都必须完成，并且Before、处理器、After均只执行一次。
                Assert.AreEqual(UIState.Showed, panels[i].State, $"第{i}层UI应完成显示");
                // 确认“beforeCounts[i]”等于1。
                Assert.AreEqual(1, beforeCounts[i], $"第{i}层Before事件应只执行一次");
                // 确认“actionCounts[i]”等于1。
                Assert.AreEqual(1, actionCounts[i], $"第{i}层显示处理器应只执行一次");
                // 确认“afterCounts[i]”等于1。
                Assert.AreEqual(1, afterCounts[i], $"第{i}层After事件应只执行一次");
            }
        }

        /// <summary>
        ///  验证既有兄弟UI可以发现运行时新增节点，并在移除组件后不保留旧引用。
        /// </summary>
        [Test]
        public void SiblingRelation_RuntimeAddAndRemove_ShouldUseCurrentHierarchy()
        {
            // 所有UI位于同一非UI容器下，默认互斥组均为零。
            var container = CreateObject("SiblingRoot");
            // 创建“firstUI”并配置测试所需的层级或表现组件。
            var firstUI = CreateUI("FirstSibling", container.transform);
            // 创建“secondUI”并配置测试所需的层级或表现组件。
            var secondUI = CreateUI("SecondSibling", container.transform);

            // 先让两个既有节点执行兄弟查询，模拟关系已经被使用过。
            firstUI.Show();
            // 提交“secondUI”的显示请求，触发实际生命周期。
            secondUI.Show();
            // 确认“firstUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, firstUI.State, "后显示的同组兄弟应隐藏先显示节点");
            // 确认“secondUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, secondUI.State, "第二个兄弟应处于显示状态");

            // 在运行时新增第三个兄弟，并让它成为当前显示节点。
            var addedUI = CreateUI("RuntimeAddedSibling", container.transform);
            // 提交“addedUI”的显示请求，触发实际生命周期。
            addedUI.Show();
            // 确认“secondUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, secondUI.State, "新增兄弟应立即参与当前互斥关系");

            // 再显示早已存在的firstUI，验证它会重新推导并隐藏新增节点。
            firstUI.Show();
            // 确认“addedUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, addedUI.State, "既有节点应发现并隐藏运行时新增兄弟");
            // 确认“firstUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, firstUI.State, "既有节点应正常完成本次显示");

            // 仅移除第三个节点的UI组件，保留其Transform验证不存在陈旧UI引用。
            Object.DestroyImmediate(addedUI);
            // 提交“firstUI”的隐藏请求，检查子树联动与终态。
            firstUI.Hide();

            // 重新显示不得访问已销毁组件，也必须保持正常状态提交。
            Assert.DoesNotThrow(firstUI.Show, "移除UI组件后兄弟查询不得访问陈旧节点");
            // 确认“firstUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, firstUI.State, "移除兄弟UI后现有节点仍应正常显示");
        }

        /// <summary>
        ///  验证运行时新增直接子UI和换父后，旧父与新父均依据当前Transform层级工作。
        /// </summary>
        [Test]
        public void ChildRelation_RuntimeAddAndReparent_ShouldUseCurrentHierarchy()
        {
            // 两个父UI放入不同非UI分支，避免父UI之间发生互斥隐藏。
            var firstBranch = CreateObject("FirstParentBranch");
            // 创建“secondBranch”并配置测试所需的层级或表现组件。
            var secondBranch = CreateObject("SecondParentBranch");
            // 创建“firstParent”并配置测试所需的层级或表现组件。
            var firstParent = CreateUI("FirstParent", firstBranch.transform);
            // 创建“secondParent”并配置测试所需的层级或表现组件。
            var secondParent = CreateUI("SecondParent", secondBranch.transform);

            // 先让旧父节点在没有子UI时执行一次完整隐藏，确保后续新增必须动态发现。
            firstParent.Show();
            // 提交“firstParent”的隐藏请求，检查子树联动与终态。
            firstParent.Hide();

            // 运行时新增直接子UI，显示子UI应递归显示其当前直接父UI。
            var childUI = CreateUI("RuntimeChild", firstParent.transform);
            // 提交“childUI”的显示请求，触发实际生命周期。
            childUI.Show();
            // 确认“firstParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, firstParent.State, "新增子UI显示时应显示当前直接父UI");

            // 父UI隐藏时应立即发现并隐藏运行时新增的直接子UI。
            firstParent.Hide();
            // 确认“childUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childUI.State, "父UI应发现并隐藏运行时新增子UI");

            // 再次显示子UI后，把它从旧父节点移动到新父节点。
            childUI.Show();
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            childUI.transform.SetParent(secondParent.transform, false);

            // 旧父节点不再拥有该子UI，隐藏旧父不能影响已经换父的子UI。
            firstParent.Hide();
            // 确认“childUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, childUI.State, "换父后旧父UI不得继续隐藏该子UI");

            // 重新触发子UI显示，使其依据新层级递归显示新父节点。
            childUI.Hide();
            // 提交“childUI”的显示请求，触发实际生命周期。
            childUI.Show();
            // 确认“firstParent”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, firstParent.State, "换父后不应重新显示旧父UI");
            // 确认“secondParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, secondParent.State, "换父后应显示新的直接父UI");

            // 新父节点隐藏必须依据当前层级找到并隐藏该子UI。
            secondParent.Hide();
            // 确认“childUI”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, childUI.State, "新父UI应隐藏当前直接子UI");
        }

        /// <summary>
        ///  验证已显示父UI换到隐藏祖先下后，显示子UI仍会修复完整祖先链。
        /// </summary>
        [Test]
        public void ParentRelation_ShownParentReparentedUnderHiddenAncestor_ShouldShowNewAncestor()
        {
            // shownParent先位于没有UI祖先的分支并进入Showed状态。
            var originalBranch = CreateObject("OriginalParentBranch");
            // 创建“shownParent”并配置测试所需的层级或表现组件。
            var shownParent = CreateUI("AlreadyShownParent", originalBranch.transform);
            // 提交“shownParent”的显示请求，触发实际生命周期。
            shownParent.Show();

            // 新祖先保持默认Hided，随后把已显示父UI整体移动到其下方。
            var ancestorBranch = CreateObject("HiddenAncestorBranch");
            // 创建“hiddenAncestor”并配置测试所需的层级或表现组件。
            var hiddenAncestor = CreateUI("HiddenAncestor", ancestorBranch.transform);
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            shownParent.transform.SetParent(hiddenAncestor.transform, false);

            // 子UI位于已显示父UI下；直接父Show会短路，因此必须依赖完整祖先快照。
            var childUI = CreateUI("ChildUnderShownParent", shownParent.transform);
            // 提交“childUI”的显示请求，触发实际生命周期。
            childUI.Show();

            // 显示子UI应先显示新祖先，再确认已显示父UI和当前子UI。
            Assert.AreEqual(UIState.Showed, hiddenAncestor.State,
                "已显示父UI换父后，显示子UI必须补齐新的隐藏祖先");
            // 确认“shownParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, shownParent.State, "换父后的直接父UI应保持Showed");
            // 确认“childUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, childUI.State, "子UI应完成显示");
        }

        /// <summary>
        ///  验证父UI只显示已标记的直接子分支，并支持多个子UI、多层级联和失活节点恢复。
        /// </summary>
        [Test]
        public void Show_MarkedChildrenAndDescendant_ShouldShowOnlyMarkedBranchesOnce()
        {
            // 使用非UI容器隔离根面板，避免测试场景中的其他UI参与互斥关系。
            var container = CreateObject("MarkedChildrenRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("MarkedChildrenParent", container.transform);

            // 三个直接子UI中只标记前两个，第三个分支必须保持隐藏。
            var firstChild = CreateUI("FirstMarkedChild", parentUI.transform);
            // 创建“secondChild”并配置测试所需的层级或表现组件。
            var secondChild = CreateUI("SecondMarkedInactiveChild", parentUI.transform);
            // 创建“skippedChild”并配置测试所需的层级或表现组件。
            var skippedChild = CreateUI("SkippedChild", parentUI.transform);
            // 将“firstChild”的随父显示配置设为true，建立当前测试条件。
            firstChild.ShowWithParent = true;
            // 将“secondChild”的随父显示配置设为true，建立当前测试条件。
            secondChild.ShowWithParent = true;

            // 已标记分支继续向下级联，未标记分支下的标记孙节点不得越级显示。
            var markedGrandchild = CreateUI("MarkedGrandchild", firstChild.transform);
            // 创建“skippedGrandchild”并配置测试所需的层级或表现组件。
            var skippedGrandchild = CreateUI("SkippedGrandchild", skippedChild.transform);
            // 将“markedGrandchild”的随父显示配置设为true，建立当前测试条件。
            markedGrandchild.ShowWithParent = true;
            // 将“skippedGrandchild”的随父显示配置设为true，建立当前测试条件。
            skippedGrandchild.ShowWithParent = true;

            // 两个需要同时显示的直接子UI必须属于不同互斥组。
            SetUIBasePrivateField(secondChild, "mutexGroup", 1);

            // 第二个目标节点模拟通过SetActive(false)完成隐藏的真实面板。
            secondChild.gameObject.SetActive(false);
            // 为“secondChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            secondChild.ShowActions += () => secondChild.gameObject.SetActive(true);

            // 为全部相关UI记录同步显示行为次数，精确检查是否出现父子递归重入。
            var panels = new[]
            {
                parentUI, firstChild, secondChild, markedGrandchild, skippedChild, skippedGrandchild
            };
            // 创建“showActionCounts”，隔离本用例的资源或观测数据。
            var showActionCounts = new int[panels.Length];
            // 按既定范围逐项采样或处理，检查边界及重复执行行为。
            for (var i = 0; i < panels.Length; i++)
            {
                // 保存“index”的基准值“i”，供后续比较或恢复。
                var index = i;
                // 为“panels[index].ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
                panels[index].ShowActions += () => showActionCounts[index]++;
            }

            // 子UI显示行为发生时，父UI仍应处于包围整棵默认子树生命周期的Showing状态。
            firstChild.ShowActions += () => Assert.AreEqual(UIState.Showing, parentUI.State,
                "默认子UI显示期间父UI应保持Showing");
            // 为“secondChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            secondChild.ShowActions += () => Assert.AreEqual(UIState.Showing, parentUI.State,
                "失活默认子UI恢复期间父UI应保持Showing");

            // 只请求父UI显示，所有已标记分支都应由UIBase内部完成级联。
            parentUI.Show();

            // 父UI、两个标记直接子UI和标记孙节点应全部完成显示。
            Assert.AreEqual(UIState.Showed, parentUI.State, "父UI应完成显示");
            // 确认“firstChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, firstChild.State, "第一个标记子UI应完成显示");
            // 确认“secondChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, secondChild.State, "第二个标记子UI应完成显示");
            // 确认“markedGrandchild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, markedGrandchild.State, "标记孙节点应沿标记分支完成显示");
            // 确认“secondChild.gameObject”的自身激活状态为真。
            Assert.IsTrue(secondChild.gameObject.activeSelf, "由隐藏行为失活的标记子UI应能重新激活");

            // 未标记直接子UI及其后代均不得被父UI越级带出。
            Assert.AreEqual(UIState.Hided, skippedChild.State, "未标记直接子UI应保持隐藏");
            // 确认“skippedGrandchild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, skippedGrandchild.State, "未标记分支下的孙节点应保持隐藏");

            // 每个已显示节点只执行一次行为，未显示分支完全没有显示副作用。
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 0, 0 }, showActionCounts,
                "随父显示不得重复进入父子生命周期或触发未标记分支");
        }

        /// <summary>
        ///  验证直接同步显示标记子UI时父UI不会重复启动该子UI或同组默认兄弟。
        /// </summary>
        [Test]
        public void Show_MarkedChildRequestedDirectly_ShouldPreferExplicitRequestWithoutReentry()
        {
            // 两个同组标记子UI用于覆盖显式请求与父默认级联发生竞争的路径。
            var container = CreateObject("DirectMarkedShowRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("DirectMarkedShowParent", container.transform);
            // 创建“requestedChild”并配置测试所需的层级或表现组件。
            var requestedChild = CreateUI("ExplicitMarkedChild", parentUI.transform);
            // 创建“defaultSibling”并配置测试所需的层级或表现组件。
            var defaultSibling = CreateUI("SameGroupDefaultSibling", parentUI.transform);
            // 将“requestedChild”的随父显示配置设为true，建立当前测试条件。
            requestedChild.ShowWithParent = true;
            // 将“defaultSibling”的随父显示配置设为true，建立当前测试条件。
            defaultSibling.ShowWithParent = true;

            // 分别记录父、显式子UI和竞争兄弟的显示行为次数。
            var parentShowCount = 0;
            // 初始化“requestedShowCount”为0，作为后续回调的观测基准。
            var requestedShowCount = 0;
            // 初始化“siblingShowCount”为0，作为后续回调的观测基准。
            var siblingShowCount = 0;
            // 为“parentUI.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.ShowActions += () => parentShowCount++;
            // 为“requestedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            requestedChild.ShowActions += () => requestedShowCount++;
            // 为“defaultSibling.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            defaultSibling.ShowActions += () => siblingShowCount++;

            // 显式显示子UI会向上显示父UI，父级联必须跳过Showing请求来源及其同组竞争项。
            requestedChild.Show();

            // 父UI和显式请求都应各完成一次，默认兄弟不得反向取代显式请求。
            Assert.AreEqual(UIState.Showed, parentUI.State, "显式子UI显示时应补齐父UI");
            // 确认“requestedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, requestedChild.State, "显式请求的子UI应完成显示");
            // 确认“defaultSibling”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, defaultSibling.State, "同组默认兄弟应让显式Showing请求优先");
            // 确认“parentShowCount”等于1。
            Assert.AreEqual(1, parentShowCount, "父UI显示行为应且只应执行一次");
            // 确认“requestedShowCount”等于1。
            Assert.AreEqual(1, requestedShowCount, "显式子UI显示行为应且只应执行一次");
            // 确认“siblingShowCount”等于0。
            Assert.AreEqual(0, siblingShowCount, "同组默认兄弟不应产生显示副作用");
        }

        /// <summary>
        ///  验证直接异步显示标记子UI时不会形成父等待子、子等待父的依赖环。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_MarkedChildRequestedDirectly_ShouldCompleteWithoutDependencyCycle()
        {
            // 使用两层默认分支覆盖显式孙UI请求沿祖先链向上显示的异步路径。
            var container = CreateObject("DirectMarkedAsyncRoot");
            // 创建“rootUI”并配置测试所需的层级或表现组件。
            var rootUI = CreateUI("DirectMarkedAsyncParent", container.transform);
            // 创建“requestedBranch”并配置测试所需的层级或表现组件。
            var requestedBranch = CreateUI("RequestedMarkedBranch", rootUI.transform);
            // 创建“defaultSibling”并配置测试所需的层级或表现组件。
            var defaultSibling = CreateUI("SameGroupAsyncDefaultSibling", rootUI.transform);
            // 创建“requestedChild”并配置测试所需的层级或表现组件。
            var requestedChild = CreateUI("ExplicitMarkedAsyncChild", requestedBranch.transform);
            // 将“requestedBranch”的随父显示配置设为true，建立当前测试条件。
            requestedBranch.ShowWithParent = true;
            // 将“requestedChild”的随父显示配置设为true，建立当前测试条件。
            requestedChild.ShowWithParent = true;
            // 将“defaultSibling”的随父显示配置设为true，建立当前测试条件。
            defaultSibling.ShowWithParent = true;

            // 所有处理器立即完成，使本用例只聚焦父子依赖关系和调用次数。
            var rootActionCount = 0;
            // 初始化“branchActionCount”为0，作为后续回调的观测基准。
            var branchActionCount = 0;
            // 初始化“requestedActionCount”为0，作为后续回调的观测基准。
            var requestedActionCount = 0;
            // 初始化“siblingActionCount”为0，作为后续回调的观测基准。
            var siblingActionCount = 0;
            // 为“rootUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            rootUI.ShowAsyncActions += _ =>
            {
                // 更新“rootActionCount”的计数，记录回调实际执行次数。
                rootActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 为“requestedBranch.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            requestedBranch.ShowAsyncActions += _ =>
            {
                // 更新“branchActionCount”的计数，记录回调实际执行次数。
                branchActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 为“requestedChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            requestedChild.ShowAsyncActions += _ =>
            {
                // 更新“requestedActionCount”的计数，记录回调实际执行次数。
                requestedActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 为“defaultSibling.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            defaultSibling.ShowAsyncActions += _ =>
            {
                // 更新“siblingActionCount”的计数，记录回调实际执行次数。
                siblingActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };

            // 捕获异步结果并通过统一超时桥接器检测潜在死锁。
            Task<Exception> showOperation = CaptureException(requestedChild.ShowAsync(CancellationToken.None));
            // 等待“WaitForTask(showOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(showOperation);

            // 整条父子链应正常完成，且同组默认兄弟不得导致链接令牌自取消。
            Assert.IsNull(showOperation.Result, $"直接异步显示标记子UI不应失败：{showOperation.Result}");
            // 确认“rootUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, rootUI.State, "异步显式请求应补齐根父UI");
            // 确认“requestedBranch”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, requestedBranch.State, "显式请求所在的默认分支应完成显示");
            // 确认“requestedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, requestedChild.State, "异步显式请求的子UI应完成显示");
            // 确认“defaultSibling”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, defaultSibling.State, "同组默认兄弟应保持隐藏");
            // 确认“rootActionCount”等于1。
            Assert.AreEqual(1, rootActionCount, "根父UI异步显示行为应执行一次");
            // 确认“branchActionCount”等于1。
            Assert.AreEqual(1, branchActionCount, "显式请求所在分支的显示行为应执行一次");
            // 确认“requestedActionCount”等于1。
            Assert.AreEqual(1, requestedActionCount, "显式子UI异步显示行为应执行一次");
            // 确认“siblingActionCount”等于0。
            Assert.AreEqual(0, siblingActionCount, "同组默认兄弟不应启动异步显示行为");
        }

        /// <summary>
        ///  验证父UI同步启动全部标记子UI的关系调度，等待全部表现并跳过未标记子UI。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_MarkedChildren_ShouldScheduleTogetherAndSkipUnmarked()
        {
            // 创建两个不同互斥组的标记子UI和一个未标记子UI。
            var container = CreateObject("MarkedChildrenAsyncRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("MarkedChildrenAsyncParent", container.transform);
            // 创建“firstChild”并配置测试所需的层级或表现组件。
            var firstChild = CreateUI("FirstMarkedAsyncChild", parentUI.transform);
            // 创建“secondChild”并配置测试所需的层级或表现组件。
            var secondChild = CreateUI("SecondMarkedAsyncChild", parentUI.transform);
            // 创建“skippedChild”并配置测试所需的层级或表现组件。
            var skippedChild = CreateUI("SkippedAsyncChild", parentUI.transform);
            // 将“firstChild”的随父显示配置设为true，建立当前测试条件。
            firstChild.ShowWithParent = true;
            // 将“secondChild”的随父显示配置设为true，建立当前测试条件。
            secondChild.ShowWithParent = true;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(secondChild, "mutexGroup", 1);

            // 第一个子UI保持挂起，用于观察第二个子UI已启动，但父After仍须等待收尾。
            var releaseFirstChild = false;
            // 初始化“firstChildStarted”为false，作为后续回调的观测基准。
            var firstChildStarted = false;
            // 初始化“secondChildActionCount”为0，作为后续回调的观测基准。
            var secondChildActionCount = 0;
            // 初始化“skippedChildActionCount”为0，作为后续回调的观测基准。
            var skippedChildActionCount = 0;
            // 初始化“parentAfterCount”为0，作为后续回调的观测基准。
            var parentAfterCount = 0;
            // 为“firstChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            firstChild.ShowAsyncActions += _ =>
            {
                // 将“firstChildStarted”设为true，建立当前测试条件。
                firstChildStarted = true;
                // 返回“UniTask.WaitUntil(() => releaseFirstChild)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseFirstChild);
            };
            // 为“secondChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            secondChild.ShowAsyncActions += _ =>
            {
                // 更新“secondChildActionCount”的计数，记录回调实际执行次数。
                secondChildActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 为“skippedChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            skippedChild.ShowAsyncActions += _ =>
            {
                // 更新“skippedChildActionCount”的计数，记录回调实际执行次数。
                skippedChildActionCount++;
                // 返回“UniTask.CompletedTask”，交给调用方继续配置或断言。
                return UniTask.CompletedTask;
            };
            // 为“parentUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.AfterUIShowedEvent += () => parentAfterCount++;

            // 启动父UI异步显示并等待一帧，使第一个子UI进入挂起处理器。
            Task<Exception> showOperation = CaptureException(parentUI.ShowAsync(CancellationToken.None));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 先保存挂起期间的观察值，再释放处理器，避免断言失败遗留永久等待任务。
            var completedWhileFirstPending = showOperation.IsCompleted;
            // 保存“secondCountWhileFirstPending”的基准值“secondChildActionCount”，供后续比较或恢复。
            var secondCountWhileFirstPending = secondChildActionCount;
            // 保存“parentAfterWhileFirstPending”的基准值“parentAfterCount”，供后续比较或恢复。
            var parentAfterWhileFirstPending = parentAfterCount;
            // 将“releaseFirstChild”设为true，建立当前测试条件。
            releaseFirstChild = true;
            // 等待“WaitForTask(showOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(showOperation);

            // 父操作必须观察第一个子UI，但第二个子UI的调度不得被动画拖延。
            Assert.IsTrue(firstChildStarted, "第一个标记子UI应启动异步显示行为");
            // 确认“completedWhileFirstPending”为假。
            Assert.IsFalse(completedWhileFirstPending, "标记子UI未完成时父ShowAsync不得提前完成");
            // 确认“secondCountWhileFirstPending”等于1。
            Assert.AreEqual(1, secondCountWhileFirstPending, "第二个标记子UI应已在同一轮同步调度中启动");
            // 确认“parentAfterWhileFirstPending”等于0。
            Assert.AreEqual(0, parentAfterWhileFirstPending, "默认子树未完成时父After不得触发");

            // 解除闸门后父UI与两个标记子UI应全部完成，未标记子UI完全不参与。
            Assert.IsNull(showOperation.Result, $"父UI异步级联显示不应失败：{showOperation.Result}");
            // 确认“parentUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parentUI.State, "父UI应在默认子树完成后提交Showed");
            // 确认“firstChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, firstChild.State, "第一个标记子UI应完成显示");
            // 确认“secondChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, secondChild.State, "第二个标记子UI应完成显示");
            // 确认“skippedChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, skippedChild.State, "未标记子UI应保持隐藏");
            // 确认“secondChildActionCount”等于1。
            Assert.AreEqual(1, secondChildActionCount, "第二个标记子UI显示行为应执行一次");
            // 确认“skippedChildActionCount”等于0。
            Assert.AreEqual(0, skippedChildActionCount, "未标记子UI不应启动显示行为");
            // 确认“parentAfterCount”等于1。
            Assert.AreEqual(1, parentAfterCount, "默认子树完成后父After应且只应触发一次");
        }

        /// <summary>
        ///  验证showWithParent只筛选父显示目标，不改变重复Show短路和父隐藏全部子UI的规则。
        /// </summary>
        [Test]
        public void ShowAndHide_MarkedAndUnmarkedChildren_ShouldPreserveExistingParentRules()
        {
            // 两个不同互斥组的子UI可以同时显示，便于验证父隐藏会处理全部直接子UI。
            var container = CreateObject("PreservedParentRulesRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("PreservedParentRulesParent", container.transform);
            // 创建“markedChild”并配置测试所需的层级或表现组件。
            var markedChild = CreateUI("MarkedChild", parentUI.transform);
            // 创建“unmarkedChild”并配置测试所需的层级或表现组件。
            var unmarkedChild = CreateUI("UnmarkedChild", parentUI.transform);
            // 将“markedChild”的随父显示配置设为true，建立当前测试条件。
            markedChild.ShowWithParent = true;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(unmarkedChild, "mutexGroup", 1);

            // 分别记录两个子UI的显示和隐藏行为次数。
            var markedShowCount = 0;
            // 初始化“markedHideCount”为0，作为后续回调的观测基准。
            var markedHideCount = 0;
            // 初始化“unmarkedShowCount”为0，作为后续回调的观测基准。
            var unmarkedShowCount = 0;
            // 初始化“unmarkedHideCount”为0，作为后续回调的观测基准。
            var unmarkedHideCount = 0;
            // 为“markedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.ShowActions += () => markedShowCount++;
            // 为“markedChild.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.HideActions += () => markedHideCount++;
            // 为“unmarkedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            unmarkedChild.ShowActions += () => unmarkedShowCount++;
            // 为“unmarkedChild.HideActions”追加观测或重入回调，验证通知次数及执行顺序。
            unmarkedChild.HideActions += () => unmarkedHideCount++;

            // 父显示只带起标记子UI，随后手动显示未标记子UI以准备父隐藏验证。
            parentUI.Show();
            // 提交“unmarkedChild”的显示请求，触发实际生命周期。
            unmarkedChild.Show();
            // 确认“markedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, markedChild.State, "标记子UI应随父显示");
            // 确认“unmarkedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, unmarkedChild.State, "未标记子UI应支持独立显示");

            // 父隐藏仍应隐藏全部直接子UI，不得只处理showWithParent节点。
            parentUI.Hide();
            // 确认“markedChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, markedChild.State, "父隐藏应处理标记子UI");
            // 确认“unmarkedChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, unmarkedChild.State, "父隐藏应处理未标记子UI");
            // 确认“markedHideCount”等于1。
            Assert.AreEqual(1, markedHideCount, "标记子UI隐藏行为应执行一次");
            // 确认“unmarkedHideCount”等于1。
            Assert.AreEqual(1, unmarkedHideCount, "未标记子UI隐藏行为应执行一次");

            // 再次显示父UI会恢复标记子UI，但未标记子UI仍保持隐藏。
            parentUI.Show();
            // 确认“markedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, markedChild.State, "父UI重新显示时应恢复标记子UI");
            // 确认“unmarkedChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, unmarkedChild.State, "父UI重新显示时不应恢复未标记子UI");

            // 手动隐藏标记子UI后，对已Showed父UI重复Show仍必须保持原有空操作语义。
            markedChild.Hide();
            // 保存“markedShowCountBeforeRepeatedParentShow”的基准值“markedShowCount”，供后续比较或恢复。
            var markedShowCountBeforeRepeatedParentShow = markedShowCount;
            // 提交“parentUI”的显示请求，触发实际生命周期。
            parentUI.Show();
            // 确认“markedChild”的界面状态等于隐藏终态。
            Assert.AreEqual(UIState.Hided, markedChild.State, "已显示父UI重复Show不应恢复手动隐藏的子UI");
            // 确认“markedShowCount”等于“markedShowCountBeforeRepeatedParentShow”。
            Assert.AreEqual(markedShowCountBeforeRepeatedParentShow, markedShowCount,
                "父UI重复Show不应重新派发标记子UI显示行为");
            // 确认“markedShowCount”等于2。
            Assert.AreEqual(2, markedShowCount, "标记子UI只应随两次真实父显示生命周期显示");
            // 确认“unmarkedShowCount”等于1。
            Assert.AreEqual(1, unmarkedShowCount, "未标记子UI只应响应一次手动显示");
        }

        /// <summary>
        ///  验证父UI和标记子UI以不同Start顺序初始化时都只执行一次显示生命周期。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Start_ParentAndMarkedChildInEitherOrder_ShouldInitializeShowOnce()
        {
            // 第一条分支让父UI先Start，用于覆盖父级联发生在子UI Start之前的路径。
            var parentFirstContainer = CreateObject("ParentFirstStartRoot");
            // 创建“parentFirst”并配置测试所需的层级或表现组件。
            var parentFirst = CreateUI("ParentFirst", parentFirstContainer.transform);
            // 创建“parentFirstChild”并配置测试所需的层级或表现组件。
            var parentFirstChild = CreateUI("ParentFirstMarkedChild", parentFirst.transform);
            // 将“parentFirstChild”的随父显示配置设为true，建立当前测试条件。
            parentFirstChild.ShowWithParent = true;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(parentFirst, "state", UIState.Showed);

            // 第二条分支让子UI先Start，用于覆盖子初态完成后再接收父级联的路径。
            var childFirstContainer = CreateObject("ChildFirstStartRoot");
            // 创建“childFirstParent”并配置测试所需的层级或表现组件。
            var childFirstParent = CreateUI("ChildFirstParent", childFirstContainer.transform);
            // 创建“childFirst”并配置测试所需的层级或表现组件。
            var childFirst = CreateUI("ChildFirstMarkedChild", childFirstParent.transform);
            // 将“childFirst”的随父显示配置设为true，建立当前测试条件。
            childFirst.ShowWithParent = true;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(childFirstParent, "state", UIState.Showed);

            // 为两条分支分别统计父子显示行为次数。
            var parentFirstShowCount = 0;
            // 初始化“parentFirstChildShowCount”为0，作为后续回调的观测基准。
            var parentFirstChildShowCount = 0;
            // 初始化“childFirstParentShowCount”为0，作为后续回调的观测基准。
            var childFirstParentShowCount = 0;
            // 初始化“childFirstShowCount”为0，作为后续回调的观测基准。
            var childFirstShowCount = 0;
            // 为“parentFirst.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentFirst.ShowActions += () => parentFirstShowCount++;
            // 为“parentFirstChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentFirstChild.ShowActions += () => parentFirstChildShowCount++;
            // 为“childFirstParent.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            childFirstParent.ShowActions += () => childFirstParentShowCount++;
            // 为“childFirst.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            childFirst.ShowActions += () => childFirstShowCount++;

            // 先启用第一条分支的父UI，等待其Start级联，再启用已经完成显示的子UI。
            parentFirst.enabled = true;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 将“parentFirstChild”的组件启用状态设为true，建立当前测试条件。
            parentFirstChild.enabled = true;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 先启用第二条分支的子UI完成Hided初始化，再启用序列化为Showed的父UI。
            childFirst.enabled = true;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 将“childFirstParent”的组件启用状态设为true，建立当前测试条件。
            childFirstParent.enabled = true;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 两种Start顺序最终都应显示完整父子链。
            Assert.AreEqual(UIState.Showed, parentFirst.State, "父先Start分支的父UI应完成显示");
            // 确认“parentFirstChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parentFirstChild.State, "父先Start分支的子UI应完成显示");
            // 确认“childFirstParent”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, childFirstParent.State, "子先Start分支的父UI应完成显示");
            // 确认“childFirst”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, childFirst.State, "子先Start分支的子UI应完成显示");

            // 初始化标记必须阻止任一分支因后续Start再次重复显示生命周期。
            Assert.AreEqual(1, parentFirstShowCount, "父先Start分支的父UI显示行为应执行一次");
            // 确认“parentFirstChildShowCount”等于1。
            Assert.AreEqual(1, parentFirstChildShowCount, "父先Start分支的子UI显示行为应执行一次");
            // 确认“childFirstParentShowCount”等于1。
            Assert.AreEqual(1, childFirstParentShowCount, "子先Start分支的父UI显示行为应执行一次");
            // 确认“childFirstShowCount”等于1。
            Assert.AreEqual(1, childFirstShowCount, "子先Start分支的子UI显示行为应执行一次");
        }

        /// <summary>
        ///  验证未完成Start且序列化为Showed的inactive子UI仍会随父显示恢复实际效果。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Show_ParentWithInactiveUninitializedShowedChild_ShouldRestoreChildOnce()
        {
            // 子UI保持未初始化状态，并模拟场景中已序列化为Showed但实际inactive的面板。
            var container = CreateObject("InactiveInitialShowedRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("InactiveInitialShowedParent", container.transform);
            // 创建“markedChild”并配置测试所需的层级或表现组件。
            var markedChild = CreateUI("InactiveInitialShowedChild", parentUI.transform);
            // 将“markedChild”的随父显示配置设为true，建立当前测试条件。
            markedChild.ShowWithParent = true;
            // 配置 UIBase 的序列化测试字段，避免为测试扩展生产接口。
            SetUIBasePrivateField(markedChild, "state", UIState.Showed);
            // 切换“markedChild.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            markedChild.gameObject.SetActive(false);
            // 将“markedChild”的组件启用状态设为true，建立当前测试条件。
            markedChild.enabled = true;

            // 显示行为模拟常见的SetActive显示交互，并统计父级联实际执行次数。
            var childShowCount = 0;
            // 为“markedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.ShowActions += () =>
            {
                // 更新“childShowCount”的计数，记录回调实际执行次数。
                childShowCount++;
                // 切换“markedChild.gameObject”的激活状态，覆盖失活与恢复时的生命周期边界。
                markedChild.gameObject.SetActive(true);
            };

            // 父显示必须忽略未初始化子UI的状态短路，主动恢复其实际显示效果。
            parentUI.Show();
            // 保存“childStateAfterParentShow”的基准值“markedChild”的界面状态，供后续比较或恢复。
            var childStateAfterParentShow = markedChild.State;
            // 保存“childActiveAfterParentShow”的基准值“markedChild.gameObject”的自身激活状态，供后续比较或恢复。
            var childActiveAfterParentShow = markedChild.gameObject.activeSelf;
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 后续Start应识别父级联已经接管初始化，不得再次重复显示生命周期。
            Assert.AreEqual(UIState.Showed, childStateAfterParentShow, "父级联后子UI应完成Showed状态提交");
            // 确认“childActiveAfterParentShow”为真。
            Assert.IsTrue(childActiveAfterParentShow, "父级联应恢复inactive子UI的实际显示效果");
            // 确认“childShowCount”等于1。
            Assert.AreEqual(1, childShowCount, "父级联和后续Start合计只应执行一次子UI显示行为");
        }

        /// <summary>
        ///  验证父UI异步显示取消时会等待已启动子行为收口，并阻止父子提交After状态。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator ShowAsync_ParentCanceledDuringMarkedChild_ShouldCancelCascadeWithoutAfter()
        {
            // 单个标记子UI使用忽略令牌的处理器，验证父操作不会遗留后台子任务。
            var container = CreateObject("CanceledMarkedChildRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("CanceledMarkedChildParent", container.transform);
            // 创建“markedChild”并配置测试所需的层级或表现组件。
            var markedChild = CreateUI("CanceledMarkedChild", parentUI.transform);
            // 将“markedChild”的随父显示配置设为true，建立当前测试条件。
            markedChild.ShowWithParent = true;
            // 初始化“releaseChild”为false，作为后续回调的观测基准。
            var releaseChild = false;
            // 初始化“childStarted”为false，作为后续回调的观测基准。
            var childStarted = false;
            // 初始化“parentAfterCount”为0，作为后续回调的观测基准。
            var parentAfterCount = 0;
            // 初始化“childAfterCount”为0，作为后续回调的观测基准。
            var childAfterCount = 0;
            // 为“markedChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.ShowAsyncActions += _ =>
            {
                // 将“childStarted”设为true，建立当前测试条件。
                childStarted = true;
                // 返回“UniTask.WaitUntil(() => releaseChild)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseChild);
            };
            // 为“parentUI.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.AfterUIShowedEvent += () => parentAfterCount++;
            // 为“markedChild.AfterUIShowedEvent”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.AfterUIShowedEvent += () => childAfterCount++;

            // 启动父显示并等待子处理器挂起后取消调用方令牌。
            using var tokenSource = new CancellationTokenSource();
            // 启动并保存显示操作（showOperation），随后检查等待、取消与完成结果。
            Task<Exception> showOperation = CaptureException(parentUI.ShowAsync(tokenSource.Token));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 取消本轮操作，验证旧任务停止提交成功状态。
            tokenSource.Cancel();
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 先记录取消后的完成状态，再释放忽略令牌的处理器，避免留下永久等待项。
            var completedBeforeChildReleased = showOperation.IsCompleted;
            // 将“releaseChild”设为true，建立当前测试条件。
            releaseChild = true;
            // 等待“WaitForTask(showOperation)”，再检查操作完成后的状态。
            yield return WaitForTask(showOperation);

            // 父操作应等待已启动子行为收口，随后以标准取消异常结束。
            Assert.IsTrue(childStarted, "标记子UI异步行为应在取消前启动");
            // 确认“completedBeforeChildReleased”为假。
            Assert.IsFalse(completedBeforeChildReleased, "子行为未结束时父操作不得提前完成");
            // 确认显示操作（showOperation）的异步结果保留取消语义，不被报告为成功。
            Assert.That(showOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "父级联显示取消后应传播OperationCanceledException");

            // 已取消的父子操作均不得提交Showed或触发After事件。
            Assert.AreNotEqual(UIState.Showed, parentUI.State, "取消后的父UI不得提交Showed");
            // 确认“markedChild”的界面状态不同于显示终态。
            Assert.AreNotEqual(UIState.Showed, markedChild.State, "取消后的标记子UI不得提交Showed");
            // 确认“parentAfterCount”等于0。
            Assert.AreEqual(0, parentAfterCount, "取消后的父UI不得触发After事件");
            // 确认“childAfterCount”等于0。
            Assert.AreEqual(0, childAfterCount, "取消后的标记子UI不得触发After事件");
        }

        /// <summary>
        ///  验证新的父显示请求会取代旧父级联中的Showing子UI，而不是错误跳过该子UI。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Show_ParentUpdateDuringMarkedChildAsync_ShouldReplaceCascadeAndReachShown()
        {
            // 标记子UI的首轮异步行为保持挂起，用于在父级联尚未完成时发起同步更新。
            var container = CreateObject("UpdatedParentCascadeRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("UpdatedParentCascadeParent", container.transform);
            // 创建“markedChild”并配置测试所需的层级或表现组件。
            var markedChild = CreateUI("UpdatedParentCascadeChild", parentUI.transform);
            // 将“markedChild”的随父显示配置设为true，建立当前测试条件。
            markedChild.ShowWithParent = true;
            // 初始化“releaseOldChildAction”为false，作为后续回调的观测基准。
            var releaseOldChildAction = false;
            // 初始化“oldChildActionStarted”为false，作为后续回调的观测基准。
            var oldChildActionStarted = false;
            // 初始化“replacementSyncActionCount”为0，作为后续回调的观测基准。
            var replacementSyncActionCount = 0;
            // 为“markedChild.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.ShowAsyncActions += _ =>
            {
                // 将“oldChildActionStarted”设为true，建立当前测试条件。
                oldChildActionStarted = true;
                // 返回“UniTask.WaitUntil(() => releaseOldChildAction)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseOldChildAction);
            };
            // 为“markedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.ShowActions += () => replacementSyncActionCount++;

            // 启动首轮父ShowAsync并等待标记子UI进入ParentCascade Showing状态。
            Task<Exception> oldParentOperation = CaptureException(parentUI.ShowAsync(CancellationToken.None));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 保存“oldChildStartedBeforeUpdate”的基准值“oldChildActionStarted”，供后续比较或恢复。
            var oldChildStartedBeforeUpdate = oldChildActionStarted;
            // 保存“oldChildStateBeforeUpdate”的基准值“markedChild”的界面状态，供后续比较或恢复。
            var oldChildStateBeforeUpdate = markedChild.State;

            // 同步父Show属于更新请求；无论显示过程是否抛出，都必须释放旧处理器门闩。
            var updatedParentState = parentUI.State;
            // 保存“updatedChildState”的基准值“markedChild”的界面状态，供后续比较或恢复。
            var updatedChildState = markedChild.State;
            // 保存“updatedSyncActionCount”的基准值“replacementSyncActionCount”，供后续比较或恢复。
            var updatedSyncActionCount = replacementSyncActionCount;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“parentUI”的显示请求，触发实际生命周期。
                parentUI.Show();
                // 将“updatedParentState”设为“parentUI”的界面状态，建立当前测试条件。
                updatedParentState = parentUI.State;
                // 将“updatedChildState”设为“markedChild”的界面状态，建立当前测试条件。
                updatedChildState = markedChild.State;
                // 将“updatedSyncActionCount”设为“replacementSyncActionCount”，建立当前测试条件。
                updatedSyncActionCount = replacementSyncActionCount;
            }
            finally
            {
                // 释放忽略取消的旧处理器，避免失败路径遗留永久轮询任务。
                releaseOldChildAction = true;
            }

            // 等待旧父操作按标准取消语义收口后再统一断言。
            yield return WaitForTask(oldParentOperation);

            // 更新前必须已经进入待替换的父级联Showing状态。
            Assert.IsTrue(oldChildStartedBeforeUpdate, "首轮父级联应启动标记子UI异步行为");
            // 确认“oldChildStateBeforeUpdate”等于显示过渡态。
            Assert.AreEqual(UIState.Showing, oldChildStateBeforeUpdate, "首轮标记子UI应处于Showing");

            // 同步更新必须真正重新显示旧ParentCascade子UI，而不是仅完成父UI自身。
            Assert.AreEqual(UIState.Showed, updatedParentState, "更新后的父UI应完成同步显示");
            // 确认“updatedChildState”等于显示终态。
            Assert.AreEqual(UIState.Showed, updatedChildState,
                "更新请求不得把旧ParentCascade Showing子UI永久跳过");
            // 确认“updatedSyncActionCount”等于1。
            Assert.AreEqual(1, updatedSyncActionCount, "更新请求应执行一次标记子UI同步显示行为");

            // 旧操作应传播取消，但不得覆盖更新请求已经提交的父子Showed状态。
            Assert.That(oldParentOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被更新请求取代的旧父级联应传播OperationCanceledException");
            // 确认“parentUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parentUI.State, "旧操作收口后不得覆盖新父UI状态");
            // 确认“markedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, markedChild.State, "旧操作收口后不得覆盖新子UI状态");
        }

        /// <summary>
        ///  验证显式子UI异步显示被取消后，新的父显示可以接管残留的Showing状态。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Show_ParentAfterExplicitChildCancellation_ShouldTakeOverMarkedChild()
        {
            // 显式显示标记子UI时让父UI异步行为保持挂起，以稳定构造子UI的Normal Showing状态。
            var container = CreateObject("CanceledExplicitChildRoot");
            // 创建“parentUI”并配置测试所需的层级或表现组件。
            var parentUI = CreateUI("CanceledExplicitChildParent", container.transform);
            // 创建“markedChild”并配置测试所需的层级或表现组件。
            var markedChild = CreateUI("CanceledExplicitChild", parentUI.transform);
            // 将“markedChild”的随父显示配置设为true，建立当前测试条件。
            markedChild.ShowWithParent = true;
            // 初始化“releaseOldParentAction”为false，作为后续回调的观测基准。
            var releaseOldParentAction = false;
            // 初始化“oldParentActionStarted”为false，作为后续回调的观测基准。
            var oldParentActionStarted = false;
            // 初始化“replacementChildShowCount”为0，作为后续回调的观测基准。
            var replacementChildShowCount = 0;
            // 为“parentUI.ShowAsyncActions”追加观测或重入回调，验证通知次数及执行顺序。
            parentUI.ShowAsyncActions += _ =>
            {
                // 将“oldParentActionStarted”设为true，建立当前测试条件。
                oldParentActionStarted = true;
                // 返回“UniTask.WaitUntil(() => releaseOldParentAction)”，交给调用方继续配置或断言。
                return UniTask.WaitUntil(() => releaseOldParentAction);
            };

            // 旧显式请求使用独立异步处理器，避免回落到同步ShowActions污染接管计数。
            markedChild.ShowAsyncActions += _ => UniTask.CompletedTask;
            // 为“markedChild.ShowActions”追加观测或重入回调，验证通知次数及执行顺序。
            markedChild.ShowActions += () => replacementChildShowCount++;

            // 子UI通过公开API发起普通显示，并在等待父UI期间取消调用方令牌。
            using var tokenSource = new CancellationTokenSource();
            // 启动并保存“oldChildOperation”，随后检查等待、取消与完成结果。
            Task<Exception> oldChildOperation = CaptureException(markedChild.ShowAsync(tokenSource.Token));
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;
            // 保存“oldParentStartedBeforeCancellation”的基准值“oldParentActionStarted”，供后续比较或恢复。
            var oldParentStartedBeforeCancellation = oldParentActionStarted;
            // 保存“oldChildStateBeforeCancellation”的基准值“markedChild”的界面状态，供后续比较或恢复。
            var oldChildStateBeforeCancellation = markedChild.State;
            // 取消本轮操作，验证旧任务停止提交成功状态。
            tokenSource.Cancel();

            // 取消已同步到链接令牌后，父同步显示必须能立即接管子UI；finally保证旧门闩总会释放。
            var updatedParentState = parentUI.State;
            // 保存“updatedChildState”的基准值“markedChild”的界面状态，供后续比较或恢复。
            var updatedChildState = markedChild.State;
            // 保存“updatedChildShowCount”的基准值“replacementChildShowCount”，供后续比较或恢复。
            var updatedChildShowCount = replacementChildShowCount;
            // 将操作与断言纳入清理范围，保证失败时也能恢复测试环境。
            try
            {
                // 提交“parentUI”的显示请求，触发实际生命周期。
                parentUI.Show();
                // 将“updatedParentState”设为“parentUI”的界面状态，建立当前测试条件。
                updatedParentState = parentUI.State;
                // 将“updatedChildState”设为“markedChild”的界面状态，建立当前测试条件。
                updatedChildState = markedChild.State;
                // 将“updatedChildShowCount”设为“replacementChildShowCount”，建立当前测试条件。
                updatedChildShowCount = replacementChildShowCount;
            }
            finally
            {
                // 旧异步处理器忽略取消令牌，必须显式允许其完成才能安全收口。
                releaseOldParentAction = true;
            }

            // 等待旧显式子显示传播标准取消异常，再检查新状态没有被旧操作覆盖。
            yield return WaitForTask(oldChildOperation);

            // 取消前必须已经构造出仍在执行的普通显示请求。
            Assert.IsTrue(oldParentStartedBeforeCancellation, "显式子显示应在取消前启动父UI异步行为");
            // 确认“oldChildStateBeforeCancellation”等于显示过渡态。
            Assert.AreEqual(UIState.Showing, oldChildStateBeforeCancellation, "取消前显式子UI应处于Showing");

            // 已取消的普通显示不再具有优先级，父级联应重新执行子UI同步显示行为。
            Assert.AreEqual(UIState.Showed, updatedParentState, "后续父UI应完成同步显示");
            // 确认“updatedChildState”等于显示终态。
            Assert.AreEqual(UIState.Showed, updatedChildState, "父级联应接管已取消子UI残留的Showing状态");
            // 确认“updatedChildShowCount”等于1。
            Assert.AreEqual(1, updatedChildShowCount, "父级联接管时应执行一次子UI同步显示行为");

            // 旧操作以取消结束，但不得覆盖更新请求已经提交的父子Showed状态。
            Assert.That(oldChildOperation.Result, Is.InstanceOf<OperationCanceledException>(),
                "被父级联取代的旧显式子显示应传播OperationCanceledException");
            // 确认“parentUI”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, parentUI.State, "旧显式操作收口后不得覆盖新父UI状态");
            // 确认“markedChild”的界面状态等于显示终态。
            Assert.AreEqual(UIState.Showed, markedChild.State, "旧显式操作收口后不得覆盖新子UI状态");
        }

        /// <summary>
        ///  验证异步隐藏接口继承隐藏生命周期基础接口，而不是显示生命周期接口。
        /// </summary>
        [Test]
        public void IHideUIAsync_ShouldInheritIHideUIBase()
        {
            // 接口继承关系属于公开编译契约，需要独立于UIBase多接口实现进行验证。
            Assert.IsTrue(typeof(IHideUIBase).IsAssignableFrom(typeof(IHideUIAsync)),
                "IHideUIAsync必须继承IHideUIBase以公开隐藏生命周期事件");
        }

        /// <summary>
        ///  验证OnStateChanged派发时State属性已经提交为回调参数中的新状态。
        /// </summary>
        [Test]
        public void OnStateChanged_CallbackShouldReadNewStateFromProperty()
        {
            // 默认Hided状态依次执行同步显示和同步隐藏，共产生四次状态变化。
            var container = CreateObject("StateChangedRoot");
            // 创建界面（ui）并配置测试所需的层级或表现组件。
            var ui = CreateUI("StateChangedUI", container.transform);
            // 创建“previousStates”，隔离本用例的资源或观测数据。
            var previousStates = new List<UIState>();
            // 创建“reportedStates”，隔离本用例的资源或观测数据。
            var reportedStates = new List<UIState>();
            // 创建“readableStates”，隔离本用例的资源或观测数据。
            var readableStates = new List<UIState>();

            // 回调内立即读取State，记录事件参数和属性观察值供统一断言。
            ui.OnStateChanged += (previousState, newState) =>
            {
                // 将本轮对象或观测值加入“previousStates”，供统一清理或顺序断言使用。
                previousStates.Add(previousState);
                // 将本轮对象或观测值加入“reportedStates”，供统一清理或顺序断言使用。
                reportedStates.Add(newState);
                // 将本轮对象或观测值加入“readableStates”，供统一清理或顺序断言使用。
                readableStates.Add(ui.State);
            };

            // 执行完整同步显隐生命周期。
            ui.Show();
            // 提交界面（ui）的隐藏请求，检查子树联动与终态。
            ui.Hide();

            // 事件参数应描述连续且完整的状态转换序列。
            CollectionAssert.AreEqual(
                new[] { UIState.Hided, UIState.Showing, UIState.Showed, UIState.Hiding },
                previousStates,
                "OnStateChanged应报告每次转换前的状态");
            // 确认“reportedStates”等于“new[] { UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided }”。
            CollectionAssert.AreEqual(
                new[] { UIState.Showing, UIState.Showed, UIState.Hiding, UIState.Hided },
                reportedStates,
                "OnStateChanged应报告每次转换后的状态");

            // 回调中的State属性必须与事件报告的新状态完全一致。
            CollectionAssert.AreEqual(reportedStates, readableStates,
                "OnStateChanged回调读取State时应得到已经提交的新状态");
        }

        /// <summary>
        ///  验证深层工具分支换父后，原Controller隐藏不会继续驱动已经进入背包的流动网格。
        /// </summary>
        /// <returns>按 Unity 测试调度逐步推进并执行断言的协程枚举器。</returns>
        [UnityTest]
        public IEnumerator Hide_AfterNestedFlowBranchMovesOut_ShouldNotDriveMovedMeshEffect()
        {
            // 工具库根先保持未激活，所有序列化依赖都在Awake和OnEnable前完整写入。
            var toolLibraryObject = new GameObject(
                "FlowToolLibrary",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(UIBase));
            // 切换“toolLibraryObject”的激活状态，覆盖失活与恢复时的生命周期边界。
            toolLibraryObject.SetActive(false);
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(toolLibraryObject);

            // Controller挂在工具库根，Content作为稳定直接子级模拟实际Prefab结构。
            var toolLibraryUI = toolLibraryObject.GetComponent<UIBase>();
            // 获取“toolLibraryCanvasGroup”的实际组件引用，供表现与生命周期断言使用。
            var toolLibraryCanvasGroup = toolLibraryObject.GetComponent<CanvasGroup>();
            // 添加并保存“flowController”，供后续生命周期与效果检查使用。
            var flowController = toolLibraryObject.AddComponent<UIFlowGroupController>();
            // 创建“contentObject”，隔离本用例的资源或观测数据。
            var contentObject = new GameObject("Content", typeof(RectTransform));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            contentObject.transform.SetParent(toolLibraryObject.transform, false);

            // ToolItem是Content深层子级，Graphic继续位于工具根内部而不是直接挂在Controller下。
            var toolItemObject = new GameObject("ToolItem", typeof(RectTransform));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            toolItemObject.transform.SetParent(contentObject.transform, false);
            // 创建“iconObject”，隔离本用例的资源或观测数据。
            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            iconObject.transform.SetParent(toolItemObject.transform, false);
            // 添加并保存“flowMeshEffect”，供后续生命周期与效果检查使用。
            var flowMeshEffect = iconObject.AddComponent<UIFlowMeshEffect>();

            // 使用UGUI内建共享材质，测试只关注归属变化并且不产生可选材质缺失警告。
            var sharedMaterial = Graphic.defaultGraphicMaterial;
            // 将“flowMeshEffect.materialPrefab”设为“sharedMaterial”，建立当前测试条件。
            flowMeshEffect.materialPrefab = sharedMaterial;
            // 设置流动组私有配置，保持与 Inspector 配置相同的入口数据。
            SetUIFlowControllerPrivateField(flowController, "materialPrefab", sharedMaterial);

            // EffectControllerBase的隐藏生命周期依赖显式UIBase和CanvasGroup缓存。
            SetEffectControllerPrivateField(flowController, "uiBase", toolLibraryUI);
            // 设置动效基类配置，构造本用例需要的动画参数。
            SetEffectControllerPrivateField(flowController, "canvasGroup", toolLibraryCanvasGroup);

            // 非退化屏幕终点保证组路径能够建立并实际接管子组件进度。
            flowController.EndScreenPoint = new Vector3(800f, 600f, 0f);

            // 背包使用另一个Canvas根，换父后原工具库不再是Graphic的直接或间接父级。
            var backpackObject = new GameObject(
                "ConstructionBackpack",
                typeof(RectTransform),
                typeof(Canvas));
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(backpackObject);
            // 创建“backpackContentObject”，隔离本用例的资源或观测数据。
            var backpackContentObject = new GameObject("Content", typeof(RectTransform));
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            backpackContentObject.transform.SetParent(backpackObject.transform, false);

            // 激活工具库并等待全部Awake、OnEnable和UIBase初始生命周期完成。
            toolLibraryObject.SetActive(true);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 工具库显示态必须把当前子组件稳定置于零进度。
            toolLibraryUI.Show();
            // 确认“flowMeshEffect”的流动进度等于0f。
            Assert.AreEqual(0f, flowMeshEffect.EffectAmount, 0.0001f,
                "工具库显示后工具Graphic应处于完整可见进度");

            // 只移动深层ToolItem根，Controller自身直接子级Content保持不变。
            toolItemObject.transform.SetParent(backpackContentObject.transform, false);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 隐藏原工具库会把自身组进度推进到一，但不得再改写已进背包的Graphic。
            toolLibraryUI.Hide();
            // 确认“flowMeshEffect”的流动进度等于0f。
            Assert.AreEqual(0f, flowMeshEffect.EffectAmount, 0.0001f,
                "原Controller隐藏后不得让已换父到背包的工具Graphic消失");

            // 再次显示工具库后把同一工具移回深层Content，模拟背包退回工具库的反向流程。
            toolLibraryUI.Show();
            // 更改实际 Transform 父级，验证后续处理使用当前关系而非旧缓存。
            toolItemObject.transform.SetParent(contentObject.transform, false);
            // 等待一帧，让 Unity 生命周期与已排队的异步续体继续推进。
            yield return null;

            // 隐藏边界必须重新发现深层新增成员，并由当前Controller统一推进到隐藏末帧。
            toolLibraryUI.Hide();
            // 确认“flowMeshEffect”的流动进度等于1f。
            Assert.AreEqual(1f, flowMeshEffect.EffectAmount, 0.0001f,
                "工具移回后必须重新加入当前Controller并参与下一次隐藏");
        }

        /// <summary>
        ///  设置EffectControllerBase仅供测试准备使用的受保护序列化字段。
        /// </summary>
        /// <typeparam name="T">目标字段的值类型。</typeparam>
        /// <param name="controller">需要配置的流动组Controller。</param>
        /// <param name="fieldName">EffectControllerBase中的字段名称。</param>
        /// <param name="value">写入目标字段的测试值。</param>
        private static void SetEffectControllerPrivateField<T>(
            UIFlowGroupController controller,
            string fieldName,
            T value)
        {
            // 反射范围固定在运行时基类，测试不会扩大生产代码的公开配置接口。
            var field = typeof(EffectControllerBase).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            // 确认“field”存在。
            Assert.IsNotNull(field, $"EffectControllerBase中不存在测试所需字段：{fieldName}");

            // 字段存在后写入精确依赖，类型不匹配由反射异常直接暴露。
            field.SetValue(controller, value);
        }

        /// <summary>
        ///  设置UIFlowGroupController仅供测试准备使用的私有序列化字段。
        /// </summary>
        /// <typeparam name="T">目标字段的值类型。</typeparam>
        /// <param name="controller">需要配置的流动组Controller。</param>
        /// <param name="fieldName">UIFlowGroupController中的字段名称。</param>
        /// <param name="value">写入目标字段的测试值。</param>
        private static void SetUIFlowControllerPrivateField<T>(
            UIFlowGroupController controller,
            string fieldName,
            T value)
        {
            // 只反射当前具体Controller私有字段，避免测试依赖编辑器序列化回调。
            var field = typeof(UIFlowGroupController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            // 确认“field”存在。
            Assert.IsNotNull(field, $"UIFlowGroupController中不存在测试所需字段：{fieldName}");

            // 字段存在后写入精确依赖，类型不匹配由反射异常直接暴露。
            field.SetValue(controller, value);
        }

        /// <summary>
        ///  设置UIBase仅供测试准备使用的私有字段。
        /// </summary>
        /// <typeparam name="T">目标字段的值类型。</typeparam>
        /// <param name="ui">需要配置的UIBase实例。</param>
        /// <param name="fieldName">UIBase中的私有字段名称。</param>
        /// <param name="value">写入目标字段的测试值。</param>
        private static void SetUIBasePrivateField<T>(UIBase ui, string fieldName, T value)
        {
            // 只在测试程序集内反射现有私有配置，避免为了测试扩大运行时公开API。
            var field = typeof(UIBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            // 确认“field”存在。
            Assert.IsNotNull(field, $"UIBase中不存在测试所需字段：{fieldName}");

            // 字段存在后写入精确测试值，类型不匹配应直接由反射异常暴露。
            field.SetValue(ui, value);
        }

        /// <summary>
        ///  创建测试GameObject并登记到统一清理集合。
        /// </summary>
        /// <param name="name">测试对象名称。</param>
        /// <param name="parent">可选的父Transform。</param>
        /// <returns>创建完成的GameObject。</returns>
        private GameObject CreateObject(string name, Transform parent = null)
        {
            // 使用普通GameObject即可验证UIBase的层级关系，无需Canvas参与。
            var createdObject = new GameObject(name);
            // 根据父界面（parent）选择对应测试路径。
            if (parent) createdObject.transform.SetParent(parent, false);
            // 将本轮对象或观测值加入待清理物体集合（_createdObjects），供统一清理或顺序断言使用。
            _createdObjects.Add(createdObject);
            // 返回“createdObject”，交给调用方继续配置或断言。
            return createdObject;
        }

        /// <summary>
        ///  创建禁用自动Start初始化的UIBase测试节点。
        /// </summary>
        /// <param name="name">UI测试对象名称。</param>
        /// <param name="parent">可选的父Transform。</param>
        /// <returns>可通过公开API驱动的UIBase实例。</returns>
        private UIBase CreateUI(string name, Transform parent = null)
        {
            // 禁用组件只阻止Unity生命周期自动调用，不影响公开方法和层级查询。
            var ui = CreateObject(name, parent).AddComponent<UIBase>();
            // 将界面（ui）的组件启用状态设为false，建立当前测试条件。
            ui.enabled = false;
            // 返回界面（ui），交给调用方继续配置或断言。
            return ui;
        }

        /// <summary>
        ///  等待UniTask并把异常转换为可在UnityTest主协程中断言的结果。
        /// </summary>
        /// <param name="task">需要观察的UniTask。</param>
        /// <returns>正常完成时返回null，失败或取消时返回对应异常。</returns>
        private static async Task<Exception> CaptureException(UniTask task)
        {
            // 捕获操作异常并返回给测试协程，便于精确断言失败类型。
            try
            {
                // 只等待一次，符合UniTask单次消费约束。
                await task;
                // 返回空引用，交给调用方继续配置或断言。
                return null;
            }
            catch (Exception exception)
            {
                // 异常交给UnityTest主协程做精确类型和副作用断言。
                return exception;
            }
        }

        /// <summary>
        ///  等待系统任务完成，并使用实时钟提供明确超时失败信息。
        /// </summary>
        /// <param name="task">需要等待的系统任务。</param>
        /// <param name="timeout">最长等待实时秒数。</param>
        /// <returns>供Unity Test Runner逐帧推进的迭代器。</returns>
        private static IEnumerator WaitForTask(Task task, float timeout = TestTimeout)
        {
            // 实时钟不受其他测试修改Time.timeScale的影响。
            var deadline = Time.realtimeSinceStartup + timeout;
            // 在截止时间内持续观察任务状态，避免测试无限等待。
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;

            // 超时通常意味着取消没有传播或组合任务遗留了未完成处理器。
            Assert.IsTrue(task.IsCompleted, $"异步测试任务未在{timeout}秒内完成");

            // CaptureException正常不会Fault，此断言用于暴露测试桥接器自身的问题。
            if (task.IsFaulted) Assert.Fail(task.Exception?.ToString());
        }

        /// <summary>
        ///  创建只会通过指定令牌取消的UniTask处理器。
        /// </summary>
        /// <param name="cancellationToken">控制任务取消的独立令牌。</param>
        /// <returns>令牌取消后抛出OperationCanceledException的任务。</returns>
        private static async UniTask WaitUntilCanceled(CancellationToken cancellationToken)
        {
            // 永不自然满足的条件确保该任务只能通过令牌结束。
            await UniTask.WaitUntil(() => false, cancellationToken: cancellationToken);
        }
    }
}
